using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WikiBot
{

    public class HttpWikiClient
    {
        /// <summary>Site's cookies.</summary>
        public CookieContainer cookies = new CookieContainer();

        /// <summary>This is a maximum degree of server load when bot is
        /// still allowed to edit pages. Higher values mean more aggressive behaviour.
        /// See <see href="https://www.mediawiki.org/wiki/Manual:Maxlag_parameter">this page</see>
        /// for details.</summary>
        public int MaxLag { get; set; } = 5;

        /// <summary>Number of times to retry bot web action in case of temporary connection
        ///  failure or some server problems.</summary>
        public int RetryCountPerRequest { get; set; } = 3;       


        public async Task<T> GetAsync<T>(string requestUri, Type[] parsers) where T : class
        {
            var resp = await GetAsync(requestUri);
            return Deserialize<T>(resp, parsers);
        }

        public async Task<T> PostAsync<T>(string requestUri, string postData, Type[] parsers) where T : class
        {
            var resp = await PostAsync(requestUri, postData);
            return Deserialize<T>(resp, parsers);            
        }

        public T Deserialize<T>(string resp, Type[] parsers) where T : class
        {
            var resType = typeof(T);

            if (resType == typeof(string))
            {
                return resp as T;
            }

            var m = parsers.SelectMany(t => t.GetMethods())
              .Where(x => x.IsPublic
              && x.IsStatic
              && x.ReturnType == resType
              && x.GetParameters().Count() == 1
              && x.GetParameters().First().ParameterType == typeof(string))
              .LastOrDefault() ?? throw new Exception("No method returns " + resType.Name);

            return m.Invoke(null, new object[] { resp }) as T;
        }


        /// <summary>Gets the text of page from web.</summary>
        /// <param name="requestUri">Absolute URI of page to get.</param>
        /// <returns>Returns source code.</returns>
        public async Task<string> GetAsync(string requestUri)
            => await MakeHttpRequestAsync(requestUri, null, false, true);

        /// <summary>gets specified string to requested resource
        /// and gets the result text.</summary>
        /// <param name="requestUri">Absolute URI of page to get.</param>        
        /// <returns>Returns text.</returns>
        public async Task<string> GetAndSaveCookiesAsync(string requestUri, bool allowRedirect = false) 
            => await MakeHttpRequestAsync(requestUri, null, true, allowRedirect);

        /// <summary>Posts specified string to requested resource
        /// and gets the result text.</summary>
        /// <param name="requestUri">Absolute URI of page to get.</param>
        /// <param name="postData">String to post to site with web request.</param>
        /// <returns>Returns text.</returns>
        public async Task<string> PostAsync(string requestUri, string postData) 
            => await MakeHttpRequestAsync(requestUri, postData, false, true);

        /// <summary>Posts specified string to requested resource
        /// and gets the result text.</summary>
        /// <param name="requestUri">Absolute URI of page to get.</param>
        /// <param name="postData">String to post to site with web request.</param>
        /// <returns>Returns text.</returns>
        public async Task<string> PostAndSaveCookiesAsync(string requestUri, string postData, bool allowRedirect = false) 
            => await MakeHttpRequestAsync(requestUri, postData, true, allowRedirect);

        /// <summary>Posts specified string to requested resource
        /// and gets the result text.</summary>
        /// <param name="requestUri">Absolute URI of page to get.</param>
        /// <param name="postData">String to post to site with web request.</param>
        /// <param name="saveCookies">If set to true, gets cookies from web response and
        /// saves it in Site.cookies container.</param>
        /// <param name="allowRedirect">Allow auto-redirection of web request by server.</param>
        /// <returns>Returns text.</returns>
        private async Task<string> MakeHttpRequestAsync(string requestUri, string postData, bool saveCookies, bool allowRedirect = false)
        {
            if (string.IsNullOrEmpty(requestUri))
            {
                throw new ArgumentNullException(nameof(requestUri), "No URL specified.");
            }

            int retryDelaySeconds = 60;
            HttpWebResponse webResp = null;

            for (int errorCounter = 0; true; errorCounter++)
            {
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(requestUri);
                webReq.Proxy.Credentials = CredentialCache.DefaultCredentials;
                webReq.UseDefaultCredentials = true;
                webReq.ContentType = "application/x-www-form-urlencoded";
                webReq.Headers.Add("Cache-Control", "no-cache, must-revalidate");
                webReq.AllowAutoRedirect = allowRedirect;
                webReq.CookieContainer = cookies;
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

                if (!string.IsNullOrEmpty(postData))
                {
                    webReq.Method = "POST";
                    postData += "&maxlag=" + this.MaxLag;
                    byte[] postBytes = Encoding.UTF8.GetBytes(postData);
                    webReq.ContentLength = postBytes.Length;

                    Stream reqStrm = await webReq.GetRequestStreamAsync();
                    await reqStrm.WriteAsync(postBytes, 0, postBytes.Length);
                    reqStrm.Close();
                }

                try
                {
                    webResp = (HttpWebResponse)(await webReq.GetResponseAsync());

                    if (webResp.Headers["Retry-After"] != null)
                    {
                        throw new WebException("Service is unavailable due to high load.");
                    }
                    // API can return HTTP code 200 (OK) along with "Retry-After"
                    break;
                }
                catch (WebException e)
                {

                    if (webResp == null)
                    {
                        throw;
                    }

                    if (!webReq.AllowAutoRedirect
                        && webResp.StatusCode == HttpStatusCode.Redirect)    // Mono bug 636219 evasion
                    {
                        return String.Empty;
                    }

                    if (e.Message.Contains("Section=ResponseStatusLine"))
                    {   // Known Squid problem
                        throw;
                    }

                    if (webResp.Headers["Retry-After"] != null)
                    {    // Server is very busy
                        if (errorCounter > this.RetryCountPerRequest)
                        {
                            throw;
                        }
                        // See https://www.mediawiki.org/wiki/Manual:Maxlag_parameter
                        Int32.TryParse(webResp.Headers["Retry-After"], out int seconds);

                        if (seconds > 0)
                        {
                            retryDelaySeconds = seconds;
                        }

                        Console.Error.WriteLine(e.Message);
                        Console.Error.WriteLine(string.Format("Retrying in {0} seconds...", retryDelaySeconds));
                        await TaskHelper.DelayInSecondsAsync(retryDelaySeconds);
                    }
                    else if (e.Status == WebExceptionStatus.ProtocolError)
                    {
                        int code = (int)webResp.StatusCode;
                        if (code == 500 || code == 502 || code == 503 || code == 504)
                        {
                            // Remote server problem, retry
                            if (errorCounter > this.RetryCountPerRequest)
                            {
                                throw;
                            }

                            Console.Error.WriteLine(e.Message);
                            Console.Error.WriteLine(string.Format("Retrying in {0} seconds...", retryDelaySeconds));

                            await TaskHelper.DelayInSecondsAsync(retryDelaySeconds);

                        }
                        else
                            throw;
                    }
                    else
                        throw;
                }
            }

            Stream respStream = webResp.GetResponseStream();
            if (webResp.ContentEncoding.ToLower().Contains("gzip"))
            {
                respStream = new GZipStream(respStream, CompressionMode.Decompress);
            }
            else if (webResp.ContentEncoding.ToLower().Contains("deflate"))
            {
                respStream = new DeflateStream(respStream, CompressionMode.Decompress);
            }

            if (saveCookies)
            {
                Uri siteUri = new Uri(requestUri);

                foreach (Cookie cookie in webResp.Cookies)
                {
                    if (cookie.Domain[0] == '.' &&
                        cookie.Domain.Substring(1) == siteUri.Host)
                    {
                        cookie.Domain = cookie.Domain.TrimStart(new char[] { '.' });
                    }

                    cookies.Add(cookie);
                }
            }

            StreamReader strmReader = new StreamReader(respStream, Encoding.UTF8);
            string respStr = await strmReader.ReadToEndAsync();
            strmReader.Close();
            webResp.Close();
            return respStr;
        }


        public async Task<string> PostMultiPartAsync(string res, byte[] fileBytes, string[] formData)
        {
            int retryDelaySeconds = 60;
            WebResponse webResp = null;

            List<string> formDataAll = new List<string>
            {
                "maxlag\"\r\n\r\n" + this.MaxLag + "\r\n",
            };

            formDataAll.AddRange(formData);

            for (int errorCounter = 0; true; errorCounter++)
            {
                string boundary = DateTime.Now.Ticks.ToString("x");
                byte[] boundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                string paramHead = "--" + boundary + "\r\nContent-Disposition: form-data; name=\"";
                byte[] postHeaderBytes = Encoding.UTF8.GetBytes(String.Join(String.Empty, formDataAll.Select(item => paramHead + item)));


                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(res);
                webReq.Proxy.Credentials = CredentialCache.DefaultCredentials;
                webReq.UseDefaultCredentials = true;
                webReq.Method = "POST";
                webReq.ContentType = "multipart/form-data; boundary=" + boundary;
                webReq.CookieContainer = this.cookies;
                webReq.CachePolicy = new System.Net.Cache.HttpRequestCachePolicy(System.Net.Cache.HttpRequestCacheLevel.Refresh);
                webReq.ContentLength = postHeaderBytes.Length + fileBytes.Length + boundaryBytes.Length;

                Stream reqStream = await webReq.GetRequestStreamAsync();
                await reqStream.WriteAsync(postHeaderBytes, 0, postHeaderBytes.Length);
                await reqStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                await reqStream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length);

                try
                {
                    webResp = (HttpWebResponse)(await webReq.GetResponseAsync());
                    break;
                }
                catch (WebException e)
                {

                    if (webResp == null)
                        throw;

                    if (e.Message.Contains("Section=ResponseStatusLine"))
                    {   // Known Squid problem
                        throw;
                    }

                    if (webResp.Headers["Retry-After"] != null)
                    {    // Server is very busy
                        if (errorCounter > this.RetryCountPerRequest)
                        {
                            throw;
                        }

                        Int32.TryParse(webResp.Headers["Retry-After"], out int seconds);

                        if (seconds > 0)
                        {
                            retryDelaySeconds = seconds;
                        }

                        await TaskHelper.DelayInSecondsAsync(retryDelaySeconds);
                    }
                    else if (e.Status == WebExceptionStatus.ProtocolError)
                    {
                        int code = (int)((HttpWebResponse)webResp).StatusCode;

                        if (code == 500
                            || code == 502
                            || code == 503
                            || code == 504)
                        {
                            // Remote server problem
                            if (errorCounter > this.RetryCountPerRequest)
                            {
                                throw;
                            }

                            await TaskHelper.DelayInSecondsAsync(retryDelaySeconds);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            StreamReader strmReader = new StreamReader(webResp.GetResponseStream());
            string respStr = await strmReader.ReadToEndAsync();
            strmReader.Close();
            webResp.Close();

            return respStr;

        }

        public async Task DownloadFileAsync(string fileLink, string destinationFilePathName)
        {
            WebClient webClient = new WebClient
            {
                UseDefaultCredentials = true,
                Encoding = Encoding.UTF8
            };

            webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            webClient.Headers.Add("Accept-Encoding", "identity");    // disallow traffic compression

            await webClient.DownloadFileTaskAsync(new Uri(fileLink), destinationFilePathName);
        }
    }

}
