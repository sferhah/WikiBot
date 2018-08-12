using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WikiBot
{
    /// <summary>Class defines wiki BotBase object. Use BotFactory.CreateInstanceAsync to create an instance. </summary>
    public class BotBase
    {
        protected HttpWikiClient client = new HttpWikiClient();

        /// <summary>Parsed API session-wide security tokens for editing.</summary>
        protected Dictionary<string, string> tokens;

        protected BotBase()
        {
            if (!new StackTrace().ToString().Contains("WikiBot.BotFactory.CreateInstanceAsync"))
            {
                throw new Exception("Please use WikiBot.BotFactory.CreateInstanceAsync to create an instance of your Bot");
            }
        }

        /// <summary>Absolute path to MediaWiki's "index.php" file on the server.</summary>
        public string IndexPath { get; private set; }

        /// <summary>Absolute path to MediaWiki's "api.php" file on the server.</summary>
        public string ApiPath => IndexPath.Replace("index.php", "api.php");

        private string _address;
        /// <summary>Site's URI.</summary>
        public string Address
        {
            get => _address;
            internal set
            {
                if (value == null)
                {
                    _address = null;
                    return;
                }

                // Correct the address if required
                if (!value.StartsWith("http"))
                {
                    value = "http://" + value;
                }

                if (value.CountMatches("/", false) == 3
                    && value.EndsWith("/"))
                {
                    value = value.Remove(value.Length - 1);
                }

                _address = value;
            }
        }

        /// <summary>User's account to login with.</summary>
        public string UserName { get; internal set; }

        /// <summary>User's password to login with.</summary>
        public string UserPass { get; internal set; }

        private string _userDomain = String.Empty;

        /// <summary>Default domain for LDAP authentication, if such authentication is allowed on
        /// this site. Additional information can be found
        /// <see href="http://www.mediawiki.org/wiki/Extension:LDAP_Authentication">here</see>.
        /// </summary>
        public string UserDomain
        {
            get => _userDomain;
            internal set
            {   
                _userDomain = value ?? String.Empty;
            }
        }

        /// <summary>Number of seconds to pause for between edits on this site.
        /// Adjust this variable if required, but it may be overriden by the site policy. Default is 5 seconds.
        /// Unflagged bots are expected to edit every 60s, flagged bots edit at a minmum interval of 5 seconds.</summary>
        /// <see href="https://meta.wikimedia.org/wiki/Bot_policy#Edit_throttle_and_peak_hours">here</see>.
        public int SaveDelay { get; set; } = 5;



        /// <summary>This is a maximum degree of server load when bot is
        /// still allowed to edit pages. Higher values mean more aggressive behaviour.
        /// See <see href="https://www.mediawiki.org/wiki/Manual:Maxlag_parameter">this page</see>
        /// for details.</summary>
        public int MaxLag
        {
            get => client.MaxLag;
            set => client.MaxLag = value;            
        }

        /// <summary>Number of times to retry bot web action in case of temporary connection
        ///  failure or some server problems.</summary>
        public int RetryCountPerRequest
        {
            get => client.RetryCountPerRequest;
            set => client.RetryCountPerRequest = value;
        }

        /// <summary>Templates, which are used to distinguish disambiguation pages. Set this
        /// variable manually if required. Multiple templates can be specified, use '|'
        /// character as the delimeter. Letters case doesn't matter.</summary>
        /// <example><code>site.disambig = "disambiguation|disambig|disam";</code></example>
        protected string disambig;

        public SiteInformation Info { get; private set; }

        /// <summary>Retuns a list of parsers used to deserialize Wikimedia objects.</summary>
        /// <exclude/>
        virtual public ICollection<Type> Parsers => new List<Type> { typeof(WikiParser) };

        /// <summary>This internal function initializes Bot object.</summary>
        /// <exclude/>
        virtual protected internal async Task InitializeAsync()
        {
            this.IndexPath = await FindApiPathAsync(Address);
            this.Info = await GetSiteInformationAsync();
            this.disambig = await GetDisambigAsync();
            this.tokens = await LogInAsync(UserName, UserPass, UserDomain);
        }

        public async Task<T> GetAsync<T>(string requestUri) where T : class
           => await client.GetAsync<T>(requestUri, this.Parsers.ToArray());

        public async Task<T> PostAsync<T>(string requestUri, string postData) where T : class
            => await client.PostAsync<T>(requestUri, postData, this.Parsers.ToArray());

        public async Task<SiteInformation> GetSiteInformationAsync()
        {
            return await GetAsync<SiteInformation>(ApiPath + "?action=query&format=xml" +
              "&meta=siteinfo&siprop=general|namespaces|namespacealiases|magicwords|" +
              "interwikimap|fileextensions|variables");
        }

        private async Task<string> FindApiPathAsync(string address)
        {
            string indexPath = null;

            string src = await client.GetAsync(address);

            try
            {
                foreach (Match m in new Regex("(?i) href=\"(([^\"]*)(index|api)\\.php)").Matches(src))
                {
                    if (m.Groups[1].Value.StartsWith(address))
                    {
                        indexPath = m.Groups[2].Value + "index.php";
                        break;
                    }
                    else if (m.Groups[1].Value.StartsWith("//" + new Uri(address).Authority))
                    {
                        if (address.StartsWith("https:"))
                            indexPath = "https:" + m.Groups[2].Value + "index.php";
                        else
                            indexPath = "http:" + m.Groups[2].Value + "index.php";
                        break;
                    }
                    else if (m.Groups[1].Value[0] == '/' && m.Groups[1].Value[1] != '/')
                    {
                        indexPath = address + m.Groups[2].Value + "index.php";
                        break;
                    }
                    else if (string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        indexPath = address + "/index.php";
                        break;
                    }
                }
            }
            catch
            {
                throw new WikiBotException("Can't find path to index.php.");
            }
            if (indexPath == null)
            {
                throw new WikiBotException("Can't find path to index.php.");
            }

            indexPath = indexPath.Replace("api.php", "index.php");
            indexPath = indexPath.Replace("mediawiki/mediawiki", "mediawiki");

            return indexPath;
        }


        private async Task<string> GetDisambigAsync()
        {
            if (!Address.Contains(".wikipedia.org"))
            {
                return null;
            }

            var disambigTemplate = String.Empty;

            // Try to get template, that English Wikipedia's "Disambiguation" interwiki points to
            if (Address.Contains("//en.wikipedia.org"))
            {
                disambigTemplate = "Template:Disambiguation";
            }
            else
            {
                string src = await client.GetAsync(ApiPath + "?format=xml&action=query" +
                    "&list=langbacklinks&lbllang=en&lbltitle=Template%3ADisambiguation");

                try
                {
                    disambigTemplate = XDocument.Parse(src).Descendants("ll").First().Attribute("title").Value;
                }
                catch
                {
                    throw new ArgumentNullException("site.disambigStr", "You need to " +
                        "manually set site.disambigStr variable before calling this function." +
                        "Please, refer to documentation for details.");
                }
            }

            string disambig = Info.Namespaces.RemoveNsPrefix(disambigTemplate, 10);

            // Get local aliases - templates that redirect to discovered disambiguation template
            string src2 = await client.GetAsync(ApiPath + "?format=xml&action=query" +
                "&list=backlinks&bllimit=500&blfilterredir=redirects&bltitle=" +
                disambigTemplate.UrlEncode());

            try
            {
                var disambigRedirects = (
                    from link in XDocument.Parse(src2).Descendants("bl")
                    select link.Attribute("title").Value
                ).ToList();

                foreach (var disambigRedirect in disambigRedirects)
                {
                    disambig += '|' + Info.Namespaces.RemoveNsPrefix(disambigRedirect, 10);
                }
            }
            catch { }    // silently continue if no alias was found

            return disambig;
        }

        /// <summary>Logs in and retrieves cookies.</summary>
        private async Task<Dictionary<string, string>> LogInAsync(string userName, string userPass, string userDomain)
        {

            string tokenXmlSrc = await client.GetAndSaveCookiesAsync(ApiPath + "?action=query&meta=tokens&type=login&format=xml");
            string loginToken = XElement.Parse(tokenXmlSrc).Element("query").Element("tokens").Attribute("logintoken").Value;

            string postData = string.Format("lgname={0}&lgpassword={1}&lgdomain={2}",
              userName.UrlEncode(), userPass.UrlEncode(),
              userDomain.UrlEncode()) + "&lgtoken=" + loginToken.UrlEncode();

            string respStr = await client.PostAndSaveCookiesAsync(ApiPath + "?action=login&format=xml", postData);

            if (!respStr.Contains("result=\"Success\""))
            {
                throw new WikiBotException("\n\n" + "Login failed. Check your username and password." + "\n");
            }


            // Load API security tokens if available
            string tokensXmlSrc = await client.GetAsync(ApiPath + "?action=query&format=xml&meta=tokens" + "&type=csrf|deleteglobalaccount|patrol|rollback|setglobalaccountstatus" + "|userrights|watch&curtimestamp");

            XElement tokensXml = XElement.Parse(tokensXmlSrc);

            if (tokensXml.Element("query") == null)
            {
                return new Dictionary<string, string>();
            }

            return (
                    from attr in tokensXml.Element("query").Element("tokens").Attributes()
                    select new
                    {
                        attrName = attr.Name.ToString(),
                        attrValue = attr.Value
                    }
                ).ToDictionary(s => s.attrName, s => s.attrValue);
        }

     }

}
