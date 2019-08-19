using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace WikiBot
{
    /// <summary>Class defines wiki Bot object. Use BotFactory.CreateInstanceAsync to create an instance. </summary>
    public partial class Bot : BotBase
    {
        /// <summary>User's watchlist. This <see cref="PageList"/> is not filled automatically when
        /// Site object is constructed, you need to call <see cref="PageList.FillFromWatchList()"/>
        /// function to fill it.</summary>
        public List<string> watchList;

        /// <summary>MediaWiki system messages (those listed on "Special:Allmessages" page),
        /// user-modified versions. This dictionary is not filled automatically when Site object
        /// is constructed, you need to call <see cref="GetMediawikiMessages()"/> 
        /// function with "true" parameter to load messages into this dictionary.</summary>
        public Dictionary<string, string> messages;

        /// <summary>Time of last page saving operation on this site expressed in UTC.
        /// This internal parameter is used to prevent server overloading.</summary>
        /// <exclude/>
        public DateTime LastWriteTime { get; protected set; } = DateTime.MinValue;


        public async Task<Compare> GetDiffAsync(long fromrev, long torev)
            => await GetAsync<Compare>(ApiPath + "?format=json&action=compare&fromrev=" + fromrev + "&torev=" + torev);

        public async Task<UserContrib[]> GetUserContibutionsAsync(string username, int limit)
         => await GetAsync<UserContrib[]>(ApiPath + "?format=xml&action=query&list=usercontribs&uclimit=" + limit + "&ucuser=" + username.UrlEncode());

        public async Task<RecentChange[]> GetRecentChangesAsync(int limit)
            => await GetAsync<RecentChange[]>(ApiPath + "?format=xml&action=query&list=recentchanges&rctype=edit|new&rcprop=title|ids|sizes|flags|user|timestamp&rclimit=" + limit);


        /// <summary>Gets MediaWiki system messages
        /// (those listed on "Special:Allmessages" page).</summary>
        /// <returns>Returns dictionary, where keys are message identifiers (all in lower case)
        /// and values are message texts.</returns>
        public async Task<Dictionary<string, string>> GetMediawikiMessagesAsync()
        {
            // there is no way to get unmodified versions via API
            // no paging is required, all messages are returned in one chunk
            string src = await client.GetAsync(ApiPath + "?action=query" + "&meta=allmessages&format=xml&amenableparser=1&amcustomised=all");
            // "&amcustomised=all" query actually brings users-modified messages

            return (
                from el in XElement.Parse(src).Descendants("message")
                select new
                {
                    id = el.Attribute("name").Value,
                    body = el.Value
                }
            ).ToDictionary(s => s.id, s => s.body);
        }

        /// <summary>Gets page titles for this PageList from results of specified custom API query.
        /// Not all queries are supported and can be parsed automatically. </summary>
        /// <param name="query">Type of query, e.g. "list=allusers" or "list=allpages".</param>
        /// <param name="queryParams">Additional query parameters, specific to the
        /// query, e.g. "cmtitle=Category:Physical%20sciences&amp;cmnamespace=0|2".
        /// Parameter values must be URL-encoded with Bot.UrlEncode() function
        /// before calling this function.</param>
        /// <param name="limit">Maximum number of resultant strings to fetch.</param>
        /// <param name="fetchRate">Number of list items to fetch at a time. This settings concerns special pages
        /// output and API lists output. Default is 500. Bot accounts are allowed to fetch
        /// up to 5000 items at a time. Adjust this number if required.</param>
        /// <example><code>
        /// GetPageTitlesFromCustomApiQueryAsync("list=categorymembers",
        /// 	"cmcategory=Physical%20sciences&amp;cmnamespace=0|14",
        /// 	int.MaxValue);
        /// </code></example>
        public async Task<List<string>> GetPageTitlesFromCustomApiQueryAsync(string query, string queryParams, int limit, int fetchRate = 500)
            => (await GetApiQueryResultAsync(query, queryParams, limit, fetchRate))
            .Where(x => x.ContainsKey("_Target"))
            .Select(title => title["_Target"].FormatTitle())
            .ToList();

        /// <summary>Gets and parses results of specified custom API query.
        /// Only some basic queries are supported and can be parsed automatically.</summary>
        /// <param name="query">Type of query, e.g. "list=logevents" or "prop=links".</param>
        /// <param name="queryParams">Additional query parameters, specific to the
        /// query. Options and their descriptions can be obtained by calling api.php on target site
        /// without parameters, e.g. http://en.wikipedia.org/w/api.php,
        /// <see href="http://en.wikipedia.org/wiki/Special:ApiSandbox">API Sandbox</see>
        /// is also very useful for experiments.
        /// Parameters' values must be URL-encoded with <see cref="CacheUtils.UrlEncode(string)"/> function
        /// before calling this function.</param>
        /// <param name="limit">Maximum number of resultant strings to fetch.</param>
        /// <example><code>
        /// GetApiQueryResult("list=categorymembers",
        /// 	"cmnamespace=0|14&amp;cmcategory=" + Bot.UrlEncode("Physical sciences"),
        /// 	int.MaxValue);
        /// </code></example>
        /// <example><code>
        /// GetApiQueryResult("list=logevents",
        /// 	"letype=patrol&amp;titles=" + Bot.UrlEncode("Physics"),
        /// 	200);
        /// </code></example>
        /// <example><code>
        /// GetApiQueryResult("prop=links",
        /// 	"titles=" + Bot.UrlEncode("Physics"),
        /// 	int.MaxValue);
        /// </code></example>
        /// <returns>List of dictionary objects is returned. Dictionary keys will contain the names
        /// of attributes of each found target element, and dictionary values will contain values
        /// of those attributes. If target element is not empty element, it's value will be
        /// included into dictionary under special "_Value" key.</returns>
        private async Task<List<Dictionary<string, string>>> GetApiQueryResultAsync(string query, string queryParams, int limit, int fetchRate)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException("query");
            }

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException("limit");
            }

            var queryXml = from el in WikiParser.CommonDataXml.Element("ApiOptions").Descendants("query")
                           where el.Value == query
                           select el;

            if (!queryXml.Any())
            {
                throw new WikiBotException(string.Format("The list \"{0}\" is not supported.", query));
            }

            string prefix = queryXml.FirstOrDefault().Attribute("prefix").Value;
            string targetTag = queryXml.FirstOrDefault().Attribute("tag").Value;
            string targetAttribute = queryXml.FirstOrDefault().Attribute("attribute").Value;

            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(targetTag))
            {
                throw new WikiBotException(string.Format("The list \"{0}\" is not supported.", query));
            }

            List<Dictionary<string, string>> results = new List<Dictionary<string, string>>();
            string continueFromAttr = prefix + "from";
            string continueAttr = prefix + "continue";
            string queryUri = ApiPath + "?format=xml&action=query&" + query + '&' + prefix + "limit=" + (limit > fetchRate ? fetchRate : limit).ToString();
            string next = String.Empty;

            do
            {
                string queryFullUri = queryUri;

                if (next != String.Empty)
                {
                    queryFullUri += '&' + prefix + "continue=" + next.UrlEncode();
                }

                string src = await client.PostAsync(queryFullUri, queryParams);

                using (XmlTextReader reader = new XmlTextReader(new StringReader(src)))
                {
                    next = String.Empty;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == targetTag)
                        {
                            Dictionary<string, string> dict = new Dictionary<string, string>();

                            if (!reader.IsEmptyElement)
                            {
                                dict["_Value"] = reader.Value.HtmlDecode();

                                if (targetAttribute == null)
                                {
                                    dict["_Target"] = dict["_Value"];
                                }
                            }

                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                dict[reader.Name] = reader.Value.HtmlDecode();

                                if (targetAttribute != null && reader.Name == targetAttribute)
                                {
                                    dict["_Target"] = dict[reader.Name];
                                }
                            }

                            results.Add(dict);
                        }
                        else if (reader.IsEmptyElement && reader[continueFromAttr] != null)
                        {
                            next = reader[continueFromAttr];
                        }
                        else if (reader.IsEmptyElement && reader[continueAttr] != null)
                        {
                            next = reader[continueAttr];
                        }
                    }
                }
            }
            while (next != String.Empty && results.Count < limit);

            if (results.Count > limit)
            {
                results.RemoveRange(limit, results.Count - limit);
            }

            return results;
        }

        /// <summary>Gets the list of all WikiMedia Foundation wiki sites as listed
        /// <see href="http://meta.wikimedia.org/wiki/Special:SiteMatrix">here</see>.</summary>
        /// <param name="officialOnly">If set to false, function also returns special and private
        /// WikiMedia projects.</param>
        /// <returns>Returns list of strings.</returns>
        public async Task<List<string>> GetWikimediaProjectsAsync(bool officialOnly)
        {
            string resp = await client.GetAsync("http://meta.wikimedia.org/wiki/Special:SiteMatrix");

            string src = officialOnly ?
                resp.GetSubstring("<a id=\"aa\" name=\"aa\">", "<a id=\"total\" name=\"total\">")
                : resp.GetSubstring("<a id=\"aa\" name=\"aa\">", "class=\"printfooter\"");

            return new Regex("<a href=\"(?://)?([^\"/]+)").Matches(src).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }      

        public async Task<Page> GetPageByTitleAsync(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                throw new WikiBotException("No title is specified for page to load.");
            }

            return await GetPageAsync("&titles=" + title.UrlEncode());
        }

        public async Task<Page> GetPageByRevisionIdAsync(long revisionId)
        {
            if (revisionId <= 0)
            {
                throw new ArgumentOutOfRangeException("revisionID", "Revision ID must be a positive Int64.");
            }

            return await GetPageAsync("&revids=" + revisionId);
        }

        /// <summary>Loads page text and metadata (last revision's ID, timestamp, comment, author,
        /// minor edit mark) from wiki site. If the page doesn't exist
        /// null is returned, no exception is thrown.</summary>
        private async Task<Page> GetPageAsync(string query_string)
        {

            var page = await GetAsync<Page>(ApiPath
                + "?action=query&prop=revisions&format=xml" + "&rvprop=content|user|userid|comment|ids|flags|timestamp"
                + query_string);

            if (page == null)
            {
                return null;
            }

            page.Content.IsReadOnly = Regex.IsMatch(page.Content.Text ?? String.Empty, @"(?is)\{\{(nobots|bots\|(allow=none|" + @"deny=(?!none)[^\}]*(" + this.UserName + @"|all)|optout=all))\}\}");

            InitPageContent(page.Content);

            return page;
        }

        private void InitPageContent(PageContent content)
        {
            content.Namespaces = this.Info.Namespaces;
            content.Regexes = this.Info.Regexes;
            content.DisambigExpression = this.disambig;
        }

        public async Task<string> GetPageHtmlAsync(string title)
        {
            try
            {
                return await this.client.GetAsync(this.IndexPath + "?title=" + title.UrlEncode());
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError
                    && ex.Response is HttpWebResponse response
                    && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }
   
        /// <summary>If this page is a redirection, this function loads the title and text
        /// of redirection target page into this Page object.</summary>
        public async Task<Page> ResolveRedirectAsync(Page page)
        {
            if (!page.Content.IsRedirect)
            {
                return page;
            }

            return await GetPageByTitleAsync(page.Content.RedirectsTo);
        } 

        /// <summary>Undoes all last edits made by last contributor.
        /// The function doesn't affect other operations
        /// like renaming or protecting.</summary>
        /// <param name="comment">Comment.</param>
        /// <param name="isMinorEdit">Minor edit mark (pass true for minor edit).</param>
        /// <returns>Returns true if last edits were undone.</returns>
        public async Task<bool> UndoLastEditsAsync(Page page, string comment, bool isMinorEdit)
        {
            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to revert.");
            }

            string lastEditor = String.Empty;

            for (int i = 50; i <= 5000; i *= 10)
            {
                var history = await GetPageHistoryAsync(page.Title, i);
                lastEditor = history[0].LastUser;

                foreach (Revision revision in history)
                {
                    if (revision.LastUser != lastEditor)
                    {
                        var previousRevision = await GetPageByRevisionIdAsync(revision.Id);
                        page.Content.Text = previousRevision.Content.Text;
                        await SavePageAsync(page, comment, isMinorEdit);
                        return true;
                    }
                }

                if (history.Length < i)
                {
                    break;
                }
            }

            return false;
        }



        /// <summary>Undoes the last edit, so page text reverts to previous contents.
        /// The function doesn't affect other actions like renaming.</summary>
        /// <param name="comment">Revert comment.</param>
        /// <param name="isMinorEdit">Minor edit mark (pass true for minor edit).</param>
        public async Task RevertPageAsync(Page page, string comment, bool isMinorEdit)
        {
            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to revert.");
            }

            var history = await GetPageHistoryAsync(page.Title, 2);

            if (history.Length != 2)
            {
                return;
            }

            var previousRevision = await GetPageByRevisionIdAsync(history[1].Id);

            page.Content.Text = previousRevision.Content.Text;

            await SavePageAsync(page, comment, isMinorEdit);
            
        }



        /// <summary>Saves <see cref="Page.Content.Text"/> contents to live wiki site.</summary>
        /// <param name="comment">Your edit comment.</param>
        /// <exception cref="InsufficientRightsException">Insufficient rights to edit
        /// this page.</exception>
        /// <exception cref="BotDisallowedException">Bot operation on this page
        /// is disallowed.</exception>
        /// <exception cref="EditConflictException">Edit conflict was detected.</exception>
        /// <exception cref="WikiBotException">Wiki-related error.</exception>
        public async Task<Page> CreatePageAsync(PageContent content, string comment)
        {
            Page page = new Page
            {
                Content = content,                
            };

            await SavePageAsync(page, comment, false);

            return page;
        }

        public PageContent CreatePageContent(string title)
        {
            var content = new PageContent
            {
                Title = title,              
            };

            InitPageContent(content);

            return content;
        }

        /// <summary>Saves specified text on page on live wiki.</summary>
        /// <param name="comment">Your edit comment.</param>
        /// <param name="isMinorEdit">Minor edit mark (true = minor edit).</param>
        /// <exception cref="InsufficientRightsException">Insufficient rights to edit
        /// this page.</exception>
        /// <exception cref="BotDisallowedException">Bot operation on this page
        /// is disallowed.</exception>
        /// <exception cref="EditConflictException">Edit conflict was detected.</exception>
        /// <exception cref="WikiBotException">Wiki-related error.</exception>
        public async Task SavePageAsync(Page page, string comment, bool isMinorEdit = true)
        {
            if (String.IsNullOrEmpty(page.Content.Text))
            {
                throw new ArgumentNullException("newText", "No text is specified for page to save.");
            }

            if (String.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to save text to.");
            }

            // Get security token for editing
            string editToken = String.Empty;

            if (this.tokens != null && this.tokens.ContainsKey("csrftoken"))
            {
                editToken = this.tokens["csrftoken"];
            }
            else
            {
                var tokens = await this.GetSecurityTokensAsync(page.Title, "edit");

                if (!tokens.ContainsKey("edittoken") 
                    || tokens["edittoken"] == String.Empty)
                {
                    throw new InsufficientRightsException(string.Format("Insufficient rights to edit page \"{0}\".", page.Title));
                }

                editToken = tokens["edittoken"];
            }

            string postData = string.Format("action=edit&title={0}&summary={1}&text={2}" +
                "&watchlist={3}{4}{5}{6}&bot=1&format=xml&token={7}",
                page.Title.UrlEncode(),
                comment.UrlEncode(),
                page.Content.Text.UrlEncode(),
                "nochange",
                isMinorEdit ? "&minor=1" : "&notminor=1",
                page.Revision.Timestamp != DateTime.MinValue ? "&basetimestamp=" + page.Revision.Timestamp.ToString("s") + "Z" : String.Empty,
                page.LastLoadTime != DateTime.MinValue ? "&starttimestamp=" + page.LastLoadTime.AddSeconds(this.Info.TimeOffsetSeconds).ToString("s") + "Z" : String.Empty,
                editToken.UrlEncode());
            

            string respStr = await this.client.PostAsync(this.ApiPath, postData);            

            if (this.SaveDelay > 0)
            {
                int secondsPassed = (int)(DateTime.UtcNow - this.LastWriteTime).TotalSeconds;

                if (this.SaveDelay > secondsPassed)
                {
                    await TaskHelper.DelayInSecondsAsync(this.SaveDelay - secondsPassed);
                }
            }

            this.LastWriteTime = DateTime.UtcNow;

            XElement respXml = XElement.Parse(respStr);

            if (respXml.Element("error") != null)
            {
                string error = respXml.Element("error").Attribute("code").Value;
                string desc = respXml.Element("error").Attribute("info").Value;

                if (error == "editconflict")
                {
                    throw new EditConflictException(string.Format("Edit conflict occurred while trying to savе page \"{0}\".", page.Title));
                }
                else if (error == "noedit")
                {
                    throw new InsufficientRightsException(string.Format("Insufficient rights to edit page \"{0}\".", page.Title));
                }
                else
                {
                    throw new WikiBotException(desc);
                }
            }
            else if (respXml.Element("edit") != null && respXml.Element("edit").Element("captcha") != null)
            {
                throw new BotDisallowedException(string.Format(
                    "Error occurred when saving page \"{0}\": " +
                    "Bot operation is not allowed for this account at \"{1}\" site.",
                    page.Title, this.Address));
            }

            page.LastLoadTime = DateTime.UtcNow;
            page.Revision.Timestamp = DateTime.MinValue;
        }

        /// <summary>Gets security tokens which are required by MediaWiki to perform page
        /// modifications.</summary>
        /// <param name="action">Type of action, that security token is required for.</param>
        /// <returns>Returns Dictionary object.</returns>
        public async Task<Dictionary<string, string>> GetSecurityTokensAsync(string title, string action)
        {
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentNullException("action");
            }

            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentNullException("title");
            }

            string src = await this.client.GetAsync(this.ApiPath + "?action=query&prop=info&intoken=" +
                action + "&inprop=protection|watched|watchers|notificationtimestamp|readable" +
                "&format=xml&titles=" + title.UrlEncode());

            var tokensXml = XElement.Parse(src).Element("query").Element("pages");

            return (
                from attr in tokensXml.Element("page").Attributes()
                select new
                {
                    attrName = attr.Name.ToString(),
                    attrValue = attr.Value
                }
            ).ToDictionary(s => s.attrName, s => s.attrValue);
        }



        /// <summary>Deletes the page. Administrator's rights are required
        /// for this action.</summary>
        /// <param name="reason">Reason for deletion.</param>
        public async Task DeletePageAsync(Page page, string reason)
        {
            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to delete.");
            }

            string token = String.Empty;

            if (this.tokens != null 
                && this.tokens.ContainsKey("csrftoken"))
            {
                token = this.tokens["csrftoken"];
            }
            else
            {
                var tokens = await this.GetSecurityTokensAsync(page.Title, "delete");

                if (tokens.ContainsKey("missing"))
                {
                    throw new WikiBotException(string.Format("Page \"{0}\" doesn't exist.", page.Title));
                }

                if (!tokens.ContainsKey("deletetoken") 
                    || tokens["deletetoken"] == String.Empty)
                {
                    throw new WikiBotException(string.Format("Unable to delete page \"{0}\".", page.Title));
                }

                token = tokens["deletetoken"];
            }

            string postData = string.Format("reason={0}&token={1}", reason.UrlEncode(), token.UrlEncode());
            string respStr = await this.client.PostAsync(this.ApiPath + "?action=delete" + "&title=" + page.Title.UrlEncode() + "&format=xml", postData);

            if (respStr.Contains("<error"))
            {
                throw new WikiBotException(string.Format("Failed to delete page \"{0}\".", page.Title));
            }            
        }


        /// <summary>Protects or unprotects the page, so only authorized group of users can edit or
        /// rename it. Changing page protection mode requires administrator (sysop)
        /// rights.</summary>
        /// <param name="editMode">Protection mode for editing this page (0 = everyone allowed
        /// to edit, 1 = only registered users are allowed, 2 = only administrators are allowed
        /// to edit).</param>
        /// <param name="renameMode">Protection mode for renaming this page (0 = everyone allowed to
        /// rename, 1 = only registered users are allowed, 2 = only administrators
        /// are allowed).</param>
        /// <param name="cascadeMode">In cascading mode all the pages, included into this page
        /// (e.g., templates or images) are also automatically protected.</param>
        /// <param name="expiryDate">Date and time, expressed in UTC, when protection expires
        /// and page becomes unprotected. Use DateTime.ToUniversalTime() method to convert local
        /// time to UTC, if necessary. Pass DateTime.MinValue to make protection indefinite.</param>
        /// <param name="reason">Reason for protecting this page.</param>
        /// <example><code>
        /// page.Protect(2, 2, false, DateTime.Now.AddDays(20), "persistent vandalism");
        /// </code></example>
        public async Task ProtectAsync(Page page, int editMode, int renameMode, bool cascadeMode, DateTime expiryDate, string reason)
        {
            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to protect.");
            }

            string errorMsg = "Only values 0, 1 and 2 are accepted. Please consult documentation.";

            if (editMode > 2 || editMode < 0)
            {
                throw new ArgumentOutOfRangeException("editMode", errorMsg);
            }

            if (renameMode > 2 || renameMode < 0)
            {
                throw new ArgumentOutOfRangeException("renameMode", errorMsg);
            }

            if (expiryDate != DateTime.MinValue && expiryDate < DateTime.Now)
            {
                throw new ArgumentOutOfRangeException("expiryDate", "Protection expiry date must be later than now.");
            }



            string token = String.Empty;

            if (this.tokens != null && this.tokens.ContainsKey("csrftoken"))
            {
                token = this.tokens["csrftoken"];
            }
            else
            {
                var tokens = await this.GetSecurityTokensAsync(page.Title, "protect");

                if (tokens.ContainsKey("missing"))
                {
                    throw new WikiBotException(string.Format("Page \"{0}\" doesn't exist.", page.Title));
                }

                if (!tokens.ContainsKey("protecttoken") 
                    || tokens["protecttoken"] == String.Empty)
                {
                    return;
                }

                token = tokens["protecttoken"];
            }

            string date = Regex.Replace(expiryDate.ToString("u"), "\\D", String.Empty);

            string postData = string.Format("token={0}&protections=edit={1}|move={2}" +
                "&cascade={3}&expiry={4}|{5}&reason={6}&watchlist=nochange",
                token.UrlEncode(),
                (editMode == 2 ? "sysop" : editMode == 1 ? "autoconfirmed" : String.Empty),
                (renameMode == 2 ? "sysop" : renameMode == 1 ? "autoconfirmed" : String.Empty),
                (cascadeMode == true ? "1" : String.Empty),
                (expiryDate == DateTime.MinValue ? String.Empty : date),
                (expiryDate == DateTime.MinValue ? String.Empty : date),
                reason.UrlEncode()
            );

            string respStr = await this.client.PostAsync(this.ApiPath + "?action=protect" + "&title=" + page.Title.UrlEncode() + "&format=xml", postData);

            if (respStr.Contains("<error"))
            {
                throw new WikiBotException(string.Format("Failed to delete page \"{0}\".", page.Title));
            }

        }


        /// <summary>Adds this page to bot account's watchlist.</summary>
        public async Task WatchPageAsync(Page page)
        {
            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to watch.");
            }

            string respStr = await this.client.GetAsync(this.ApiPath + "?format=xml&action=query&meta=tokens&type=watch" + "&titles=" + page.Title.UrlEncode());
            string securityToken = String.Empty;
            string titleFallback = String.Empty;

            try
            {
                securityToken = XElement.Parse(respStr).Element("query")
                    .Element("tokens").Attribute("watchtoken").Value.ToString();
            }
            catch
            {    // FALLBACK for older version
                respStr = await this.client.GetAsync(this.ApiPath + "?format=xml&action=query&prop=info&intoken=watch" + "&titles=" + page.Title.UrlEncode());

                securityToken = XElement.Parse(respStr).Element("query").Element("pages")
                    .Element("page").Attribute("watchtoken").Value.ToString();

                titleFallback = "&title=" + page.Title.UrlEncode();
            }

            string postData = string.Format("titles={0}{1}&action=watch&token={2}&format=xml",
                page.Title.UrlEncode(), titleFallback,
                securityToken.UrlEncode());

            await this.client.PostAsync(this.ApiPath, postData);

            page.Watched = true;

            if (this.watchList == null)
            {
                this.watchList = await this.GetWatchListAsync();
            }

            if (!this.watchList.Contains(page.Title))
            {
                this.watchList.Add(page.Title);
            }

        }



        /// <summary>Removes page from bot account's watchlist.</summary>
        public async Task UnwatchPageAsync(Page page)
        {
            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to unwatch.");
            }

            string respStr = await this.client.GetAsync(this.ApiPath + "?format=xml&action=query&meta=tokens&type=watch" + "&titles=" + page.Title.UrlEncode());
            string securityToken = String.Empty;
            string titleFallback = String.Empty;

            try
            {
                securityToken = XElement.Parse(respStr).Element("query")
                    .Element("tokens").Attribute("watchtoken").Value.ToString();
            }
            catch
            {    // FALLBACK for older version
                respStr = await this.client.GetAsync(this.ApiPath + "?format=xml&action=query&prop=info&intoken=watch" + "&titles=" + page.Title.UrlEncode());
                securityToken = XElement.Parse(respStr).Element("query").Element("pages").Element("page").Attribute("watchtoken").Value.ToString();
                titleFallback = "&title=" + page.Title.UrlEncode();
            }

            string postData = string.Format("titles={0}{1}&token={2}" + "&format=xml&action=watch&unwatch=1",
                page.Title.UrlEncode(),
                titleFallback,
                securityToken.UrlEncode());

            await this.client.PostAsync(this.ApiPath, postData);

            page.Watched = false;

            if (this.watchList?.Contains(page.Title) ?? false)
            {
                for (int i = 0; i < this.watchList.Count; i++)
                {
                    if (this.watchList[i] == page.Title)
                    {
                        this.watchList.RemoveAt(i);
                    }
                }
            }
        }


        /// <summary>Renames the page. Redirection from old title to new title is
        /// automatically created.</summary>
        /// <param name="newTitle">New title of this page.</param>
        /// <param name="reason">Reason for renaming.</param>
        /// <param name="renameTalkPage">If true, corresponding talk page will
        /// also be renamed.</param>
        /// <param name="renameSubPages">If true, subpages (like User:Me/Subpage)
        /// will also be renamed.</param>
        public async Task RenamePageToAsync(Page page, string newTitle, string reason, bool renameTalkPage = false, bool renameSubPages = false)
        {
            if (string.IsNullOrEmpty(newTitle))
            {
                throw new ArgumentNullException("newTitle");
            }

            if (string.IsNullOrEmpty(page.Title))
            {
                throw new WikiBotException("No title is specified for page to rename.");
            }

            string token = String.Empty;

            if (this.tokens?.ContainsKey("csrftoken") ?? false)
            {
                token = this.tokens["csrftoken"];
            }
            else
            {
                var tokens = await this.GetSecurityTokensAsync(page.Title, "move");

                if (tokens.ContainsKey("missing"))
                {
                    throw new WikiBotException(string.Format("Page \"{0}\" doesn't exist.", page.Title));
                }

                if (!tokens.ContainsKey("movetoken") 
                    || tokens["movetoken"] == String.Empty)
                {
                    throw new WikiBotException(string.Format("Unable to rename page \"{0}\" to \"{1}\".", page.Title, newTitle));
                }

                token = tokens["movetoken"];
            }

            string postData = string.Format("from={0}&to={1}&reason={2}{3}{4}&token={5}",
                page.Title.UrlEncode(),
                newTitle.UrlEncode(),
                reason.UrlEncode(),
                renameTalkPage ? "&movetalk=1" : String.Empty,
                renameSubPages ? "&movesubpages=1" : String.Empty,
                token.UrlEncode());

            string respStr = await this.client.PostAsync(this.ApiPath + "?action=move" + "&format=xml", postData);

            if (respStr.Contains("<error"))
            {
                throw new WikiBotException(string.Format("Failed to rename page \"{0}\" to \"{1}\".", page.Title, newTitle));
            }

            page.Content.Title = newTitle;

        }


        /// <summary>Gets page history and fills this PageList with specified number of recent page
        /// revisions. Pre-existing pages will be removed from this PageList.
        /// Only revision identifiers, user names, timestamps and comments are
        /// loaded, not the texts. Call <see cref="PageList.Load()"/> to load the texts of page
        /// revisions. PageList[0] will be the most recent revision.</summary>
        /// <param name="pageTitle">Page to get history of.</param>
        /// <param name="limit">Number of last page revisions to get.</param>
        public async Task<Revision[]> GetPageHistoryAsync(string pageTitle, int limit)
        {
            if (string.IsNullOrEmpty(pageTitle))
            {
                throw new ArgumentNullException("pageTitle");
            }

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException("limit");
            }
            
            return await this.GetAsync<Revision[]>(this.ApiPath
                                                   + "?action=query&prop=revisions&titles="
                                                   + pageTitle.UrlEncode() 
                                                   + "&rvprop=ids|user|comment|timestamp" 
                                                   + "&format=xml&rvlimit=" + limit.ToString());
        }


        /// <summary>Gets page titles for this PageList from watchlist
        /// of bot account. The function does not remove redirecting
        /// pages from the results. Call <see cref="PageList.RemoveRedirects()"/> manually,
        /// if you need that. And the function neither filters namespaces, nor clears the
        /// existing PageList, so new titles will be added to the existing in PageList.</summary>
        public async Task<List<string>> GetWatchListAsync()
        {
            string src = await this.client.GetAsync(this.IndexPath + "?title=Special:Watchlist/edit");            

            return Info.Regexes.TitleLinkShown.Matches(src).Cast<Match>()
                .Select(m => m.Groups[1].Value.HtmlDecode().FormatTitle())
                .ToList();            
        }



        /// <summary>Downloads image, audio or video file, pointed by this page's title,
        /// from the wiki site to local computer. Redirection is resolved automatically.</summary>
        /// <param name="destinationFilePathName">Path and name of local file to save image to.</param>
        public async Task DownloadImageAsync(string title, string destinationFilePathName)
        {
            string src = await this.GetPageHtmlAsync(title);

            if(src == null)
            {
                return;
            }
         

            Regex fileLinkRegex1 = new Regex("<a href=\"([^\"]+?)\" class=\"internal\"");
            Regex fileLinkRegex2 = new Regex("<div class=\"fullImageLink\" id=\"file\"><a href=\"([^\"]+?)\"");

            string fileLink = "";

            if (fileLinkRegex1.IsMatch(src))
            {
                fileLink = fileLinkRegex1.Match(src).Groups[1].ToString();
            }
            else if (fileLinkRegex2.IsMatch(src))
            {
                fileLink = fileLinkRegex2.Match(src).Groups[1].ToString();
            }
            else
            {
                throw new WikiBotException(string.Format("Image \"{0}\" doesn't exist.", title));
            }

            if (!fileLink.StartsWith("http"))
            {
                fileLink = new Uri(new Uri(this.Address), fileLink).ToString();
            }      

            await client.DownloadFileAsync(fileLink, destinationFilePathName);
        }

        

        /// <summary>Uploads local image to wiki site. Function also works with non-image files.
        /// Note: uploaded image title (wiki page title) will be the same as title of this Page
        /// object, not the title of source file.</summary>
        /// <param name="filePathName">Path and name of local file.</param>
        /// <param name="description">File (image) description.</param>
        /// <param name="license">File license type (may be template title). Used only on
        /// some wiki sites. Pass empty string, if the wiki site doesn't require it.</param>
        /// <param name="copyStatus">File (image) copy status. Used only on some wiki sites. Pass
        /// empty string, if the wiki site doesn't require it.</param>
        /// <param name="source">File (image) source. Used only on some wiki sites. Pass
        /// empty string, if the wiki site doesn't require it.</param>
        public async Task<Page> UploadImageAsync(string title,
            string filePathName,
            string description,
            string license,
            string copyStatus,
            string source)
        {
            if (!File.Exists(filePathName))
            {
                throw new ArgumentNullException("filePathName", string.Format("Image file \"{0}\" doesn't exist.", filePathName));
            }

            if (string.IsNullOrEmpty(title))
            {
                throw new WikiBotException("No title is specified for image to upload.");
            }

            if (Path.GetFileNameWithoutExtension(filePathName).Length < 3)
            {
                throw new WikiBotException(string.Format("Name of file \"{0}\" must " + "contain at least 3 characters (excluding extension) for successful upload.", filePathName));
            }


            var tokens = await this.GetSecurityTokensAsync(title, "edit");    // there is no more specific token type

            if (!tokens.ContainsKey("edittoken") 
                || tokens["edittoken"] == String.Empty)
            {
                throw new WikiBotException(string.Format("Error occurred when uploading image \"{0}\".", title));
            }

            string targetName = this.Info.Namespaces.RemoveNsPrefix(title, 6).Capitalize();

            string res = this.IndexPath + "?title=" + HttpUtility.HtmlEncode(this.Info.Namespaces.GetNsPrefix(-1)) + "Upload";

            byte[] fileBytes = File.ReadAllBytes(filePathName);

            string[] formData = new string[]
            {
                "wpIgnoreWarning\"\r\n\r\n1\r\n",
                "wpDestFile\"\r\n\r\n" + targetName + "\r\n",
                "wpUploadAffirm\"\r\n\r\n1\r\n",
                "wpWatchthis\"\r\n\r\n0\r\n",
                "wpEditToken\"\r\n\r\n" + tokens["edittoken"] + "\r\n",
                "wpUploadCopyStatus\"\r\n\r\n" + copyStatus + "\r\n",
                "wpUploadSource\"\r\n\r\n" + source + "\r\n",
                "wpUpload\"\r\n\r\n" + "upload bestand" + "\r\n",
                "wpLicense\"\r\n\r\n" + license + "\r\n",
                "wpUploadDescription\"\r\n\r\n" + description + "\r\n",
                "wpUploadFile\"; filename=\"" + Path.GetFileName(filePathName).UrlEncode() + "\"\r\n" + "Content-Type: application/octet-stream\r\n\r\n",
            };

            var respStr = await client.PostMultiPartAsync(res, fileBytes, formData);

            if (!respStr.Contains(HttpUtility.HtmlEncode(targetName)))
            {
                throw new WikiBotException(string.Format("Error occurred when uploading image \"{0}\".", title));
            }

            try
            {
                if (this.messages == null)
                {
                    this.messages = await this.GetMediawikiMessagesAsync();
                }

                string errorMessage = this.messages["uploaderror"];

                if (respStr.Contains(errorMessage))
                {
                    throw new WikiBotException(string.Format("Error occurred when uploading image \"{0}\".", title));
                }
            }
            catch (WikiBotException e)
            {
                if (!e.Message.Contains("Uploadcorrupt"))    // skip if MediaWiki message not found
                    throw;
            }           

            string targetTitle = this.Info.Namespaces.GetNsPrefix(6) + targetName + Path.GetExtension(filePathName);
            return await GetPageByTitleAsync(targetTitle.FormatTitle());
        }        
    


        /// <summary>Returns list of strings, containing category names found in
        /// page's text and added by page's templates.</summary>
        /// <returns>Category names with namespace prefixes (e.g. "Category:Art").</returns>
        public async Task<List<string>> GetAllCategoriesAsync(string title)
        {

            string src = await this.client.GetAsync(this.ApiPath 
                + "?action=query&prop=categories"
                + "&clprop=sortkey|hidden&cllimit=5000&format=xml&titles="
                + title.UrlEncode());

            int startPos = src.IndexOf("<!-- start content -->");
            int endPos = src.IndexOf("<!-- end content -->");

            if (startPos != -1 && endPos != -1 && startPos < endPos)
            {
                src = src.Remove(startPos, endPos - startPos);
            }
            else
            {
                startPos = src.IndexOf("<!-- bodytext -->");
                endPos = src.IndexOf("<!-- /bodytext -->");
                if (startPos != -1 && endPos != -1 && startPos < endPos)
                {
                    src = src.Remove(startPos, endPos - startPos);
                }
            }

            XmlNamespaceManager xmlNs = new XmlNamespaceManager(new NameTable());
            xmlNs.AddNamespace("ns", "http://www.w3.org/1999/xhtml");

            XPathNodeIterator iterator = XmlHelper.GetXMLIterator(src, "//categories/cl/@title", xmlNs);

            List<string> matchStrings = new List<string>();

            iterator.MoveNext();

            for (int i = 0; i < iterator.Count; i++)
            {
                matchStrings.Add(this.Info.Namespaces.GetNsPrefix(14) + this.Info.Namespaces.RemoveNsPrefix(iterator.Current.Value.HtmlDecode(), 14));
                iterator.MoveNext();
            }

            return matchStrings;
        }


        /// <summary>For pages of Wikimedia foundation projects this function returns
        /// interlanguage links located on <see href="https://wikidata.org">
        /// Wikidata.org</see>.</summary>
        /// <returns>Returns the List&lt;string&gt; object.</returns>
        public async Task<List<string>> GetWikidataLinksAsync(string title)
        {
            string src = await this.GetPageHtmlAsync(title);            

            if (!src.Contains("<li class=\"interlanguage-link "))
            {
                return new List<string>();
            }

            src = "<ul>" + src.GetSubstring("<li class=\"interlanguage-link ", "</ul>");

            return Regex.Matches(src, "interlanguage-link interwiki-([^\" ]+)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }




        /// <summary>Finds all wikilinks in page text, excluding interwiki
        /// links, categories, embedded images and links in image descriptions.</summary>
        /// <returns>Returns the PageList object, in which page titles are the wikilinks,
        /// found in text.</returns>
        public async Task<ICollection<string>> GetPageLinksAsync(Page page)
        {
            MatchCollection matches = this.Info.Regexes.WikiLink.Matches(page.Content.Text);
            var exclLinks = await this.GetSisterwikiLinksAsync(page.Title);
            exclLinks.AddRange(await this.GetInterLanguageLinksAsync(page.Title));
            List<string> pl = new List<string>();

            for (int i = 0; i < matches.Count; i++)
            {
                string str = matches[i].Groups["title"].Value;

                if (str.StartsWith(this.Info.Namespaces.GetNsPrefix(6), true, this.Info.LangCulture) // image
                    || str.StartsWith(this.Info.Namespaces.GetEnglishNsPrefix(6), true, this.Info.LangCulture) 
                    || str.StartsWith(this.Info.Namespaces.GetNsPrefix(14), true, this.Info.LangCulture)  // category
                    || str.StartsWith(this.Info.Namespaces.GetEnglishNsPrefix(14), true, this.Info.LangCulture))
                {
                    continue;
                }

                str = str.TrimStart(':');

                if (exclLinks.Contains(str))
                {
                    continue;
                }

                int fragmentPosition = str.IndexOf("#");

                if (fragmentPosition == 0)    // in-page section link
                {
                    continue;
                }
                else if (fragmentPosition != -1)
                {
                    str = str.Substring(0, fragmentPosition);
                }

                pl.Add(str.FormatTitle());
            }

            return pl;
        }

        /// <summary>Returns links to sister wiki projects, found in this page's text. These may
        /// include interlanguage links but only those embedded in text, not those located 
        /// on wikidata.org</summary>
        /// <returns>Returns the List&lt;string&gt; object.</returns>
        public async Task<List<string>> GetSisterwikiLinksAsync(string title)
        {
            string src = await this.client.GetAsync(this.ApiPath + "?action=query&prop=iwlinks&format=xml&iwlimit=5000&titles=" + title.UrlEncode());

            return (
                from el in XDocument.Parse(src).Descendants("ns")
                select el.Attribute("prefix").Value + '|' + el.Value
            ).ToList();
        }


        /// <summary>Gets interlanguage links for pages on WikiMedia Foundation's
        /// projects.</summary>
        /// <remarks>WARNING: Because of WikiMedia software bug, this function does not work
        /// properly on main pages of WikiMedia Foundation's projects.</remarks>
        /// <returns>Returns Listof strings. Each string contains page title 
        /// prefixed with language code and colon, e.g. "de:Stern".</returns>
        public async Task<List<string>> GetInterLanguageLinksAsync(string title)
        {
            string src = await this.client.GetAsync(this.ApiPath + "?format=xml&action=query&prop=langlinks&lllimit=500&titles=" + title.UrlEncode());

            return (
                from link in XDocument.Parse(src).Descendants("ll")
                select link.Attribute("lang").Value + ':' + link.Value
            ).ToList();
        }






        /// <summary>For pages that have associated items on <see href="https://wikidata.org">
        /// Wikidata.org</see> this function returns
        /// XElement object with all information provided by Wikidata.
        /// If page is not associated with a Wikidata item null is returned.</summary>
        /// <returns>Returns XElement object or null.</returns>
        /// <example><code>
        /// Page p = new Page(enWikipedia, "Douglas Adams");
        /// XElement wikidataItem = p.GetWikidataItem();
        /// string description = (from desc in wikidataItem.Descendants("description")
        ///				          where desc.Attribute("language").Value == "en"
        ///				          select desc.Attribute("value").Value).FirstOrDefault();
        /// </code></example>
        public async Task<XElement> GetWikidataItemAsync(string title)
        {
            string src = await this.GetPageHtmlAsync(title);

            Match m = Regex.Match(src, "href=\"//www\\.wikidata\\.org/wiki/(Q\\d+)");

            if (!m.Success)    // fallback
            {
                m = Regex.Match(src, "\"wgWikibaseItemId\"\\:\"(Q\\d+)\"");
            }

            if (!m.Success)
            {
                Console.WriteLine(string.Format("No Wikidata item is associated with page \"{0}\".", title));
                return null;
            }

            string item = m.Groups[1].Value;
            string xmlSrc = await this.client.GetAsync("http://www.wikidata.org/wiki/Special:EntityData/" + item.UrlEncode() + ".xml");    // raises "404: Not found" if not found
            return XElement.Parse(xmlSrc);
        }

        public async Task<WikidataItem> GetWikidataItemAsync(string site, string title)
        {
            string resp = await client.GetAsync($"https://www.wikidata.org/w/api.php?action=wbgetentities&format=xml&sites={site}&titles={title.UrlEncode()}&normalize=true&props=infositelinks");
            var xml = XElement.Parse(resp);

            var item = new WikidataItem
            {
                Id = xml.Descendants("entity").First().Attribute("id").Value,
                Sitelinks = xml.Descendants("sitelink").Select(x => new Tuple<string, string>(x.Attribute("site").Value, x.Attribute("title").Value)).ToList(),
            };

            return item.Id == "-1" ? null : item;
        }

        public async Task<string> MergeWikidataItemsAsync(string fromItemId, string toItemId)
        {
            string resp = await GetAsync<string>(ApiPath + "?action=query&format=xml&meta=userinfo%7Ctokens");
            
            var token = XElement.Parse(resp).Descendants("tokens").First().Attribute("csrftoken").Value;

            return await PostAsync<string>(ApiPath, $"action=wbmergeitems&format=xml&fromid={fromItemId}&toid={toItemId}&bot=1&token={token.UrlEncode()}");
        }

        public async Task<string> PurgCacheAsync(string title)
        {
            return await PostAsync<string>(ApiPath, $"action=purge&format=json&formatversion=2&titles={title.UrlEncode()}");
        }

    }
}
