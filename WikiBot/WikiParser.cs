using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace WikiBot
{
    public static class WikiParser
    {
        private static XElement _commonDataXml;
        /// <summary>Some unparsed supplementary data. You can see it
        /// <see href="https://sourceforge.net/p/dotnetwikibot/svn/HEAD/tree/cache/CommonData.xml">
        /// here.</see></summary>
        public static XElement CommonDataXml
        {
            get
            {
                if (_commonDataXml != null)
                {
                    return _commonDataXml;
                }

                // Load general info cache
                using (Stream reader = CacheUtils.GetCacheStream(CacheUtils.CommonDataXmlFileName))
                {
                    return _commonDataXml = XElement.Load(reader);
                }
            }
        }

        
        [JsonObject("api")]
        public class ApiCompare 
        {
            [JsonProperty("compare")]
            public Compare Result { get; set; }
        }

        public static Compare DeserializeCompare(string src)
        {
            return JsonConvert.DeserializeObject<ApiCompare>(src).Result;
        }

        public static Revision[] DeserializePageHistory(string src)
        {
            List<Revision> history = new List<Revision>();

            using (XmlReader reader = XmlReader.Create(new StringReader(src)))
            {
                reader.ReadToFollowing("api");
                reader.Read();

                //if (reader.Name == "error")
                //{
                //    Console.Error.WriteLine("Error: {0}", reader.GetAttribute("info"));
                //}

                while (reader.ReadToFollowing("rev"))
                {
                    long.TryParse(reader.GetAttribute("revid"), out long revisionId);                  

                    history.Add(new Revision
                    {
                        Id = revisionId,
                        LastUser = reader.GetAttribute("user"),
                        Comment = reader.GetAttribute("comment"),
                        Timestamp = DateTime.Parse(reader.GetAttribute("timestamp")).ToUniversalTime()
                    });
                }
            }

            return history.ToArray();
        }

        public static RecentChange[] DeserializeRecentChanges(string src)
        {
            List<RecentChange> items = new List<RecentChange>();

            using (XmlReader reader = XmlReader.Create(new StringReader(src)))
            {
                reader.ReadToFollowing("api");
                reader.Read();           

                while (reader.ReadToFollowing("rc"))
                {
                    long.TryParse(reader.GetAttribute("rcid"), out long rcid);
                    long.TryParse(reader.GetAttribute("old_revid"), out long old_revid);
                    long.TryParse(reader.GetAttribute("revid"), out long revid);

                    items.Add(new RecentChange
                    {                        
                        Id = rcid,
                        User = reader.GetAttribute("user"),
                        Title = reader.GetAttribute("title"),
                        Type = reader.GetAttribute("type"),
                        Timestamp = DateTime.Parse(reader.GetAttribute("timestamp")).ToUniversalTime(),
                        OldRevid = old_revid,
                        Revid = revid,
                    });
                }
            }

            return items.ToArray();
        }

        public static UserContrib[] DeserializeUserContribs(string src)
        {
            List<UserContrib> contribs = new List<UserContrib>();

            using (XmlReader reader = XmlReader.Create(new StringReader(src)))
            {
                reader.ReadToFollowing("api");
                reader.Read();

                while (reader.ReadToFollowing("item"))
                {
                    int.TryParse(reader.GetAttribute("ns"), out int ns);

                    contribs.Add(new UserContrib
                    {
                        Namespace = ns,
                        Title = reader.GetAttribute("title"),
                        Comment = reader.GetAttribute("comment"),
                        Timestamp = DateTime.Parse(reader.GetAttribute("timestamp")).ToUniversalTime()
                    });
                }
            }

            return contribs.ToArray();
        }

        /// <summary>Parses general information about the site.</summary>
        /// <exclude/>
        public static SiteInformation DeserializeGeneralInfo(string src)
        {
            XElement generalDataXml = XElement.Parse(src).Element("query");

            // Load namespaces
            var namespaces = (
                from el in generalDataXml.Element("namespaces").Descendants("ns")
                select new
                {
                    code = int.Parse(el.Attribute("id").Value),
                    name = ('|' + (el.IsEmpty ? String.Empty : el.Value) +
                            '|' + (!el.IsEmpty && el.Value != el.Attribute("canonical").Value
                                    ? el.Attribute("canonical").Value + '|' : String.Empty)
                            ).ToString()
                }
            ).ToDictionary(s => s.code, s => s.name);

            // Load and add namespace aliases
            var aliases = (
                from el in generalDataXml.Element("namespacealiases").Descendants("ns")
                select new
                {
                    code = int.Parse(el.Attribute("id").Value),
                    name = el.Value.ToString()
                }
            );

            foreach (var alias in aliases)
            {
                namespaces[alias.code] += alias.name + '|';
            }


            var wikiNamespaceDictionary = new WikiNamespaceDictionary(namespaces);
            // namespace 0 may have an alias (!)


            Dictionary<string, string> generalData = (
                from attr in generalDataXml.Element("general").Attributes()
                select new
                {
                    attrName = attr.Name.ToString(),
                    attrValue = attr.Value
                }
            ).ToDictionary(s => s.attrName, s => s.attrValue);

            // Load interwiki which are recognized locally, interlanguage links are included
            // Prefixes are combined into string delimited by '|'
            generalData["interwiki"] = string.Join("|", (
                from el in generalDataXml.Descendants("iw")
                select el.Attribute("prefix").Value
            ).ToArray());

            // Load MediaWiki variables (https://www.mediawiki.org/wiki/Help:Magic_words)
            // These are used in double curly brackets, like {{CURRENTVERSION}} and must
            // be distinguished from templates.
            // Variables are combined into string delimited by '|'.
            generalData["variables"] = string.Join("|", (
                from el in generalDataXml.Descendants("v")
                select el.Value
            ).ToArray());

            // Load MediaWiki magic words (https://www.mediawiki.org/wiki/Help:Magic_words)
            // These include MediaWiki variables and parser functions which are used in
            // double curly brackets, like {{padleft:xyz|stringlength}} or
            // {{#formatdate:date}} and must be distinguished from templates.
            // Magic words are combined into string delimited by '|'.
            generalData["magicWords"] = string.Join("|", (
                from el in generalDataXml.Element("magicwords").Descendants("alias")
                select el.Value
            ).ToArray());


            string shortPath = null;
            // Set Site object's properties
            if (generalData.ContainsKey("articlepath"))
            {
                shortPath = generalData["articlepath"].Replace("$1", String.Empty);
            }

            Version version = generalData.ContainsKey("generator") ? new Version(Regex.Replace(generalData["generator"], @"[^\d\.]", String.Empty)) : null;


            string language = generalData["lang"];
            string capitalization = generalData["case"];
            string timeOffset = generalData["timeoffset"];
            string name = generalData["sitename"];
            string software = generalData["generator"];

            DateTime wikiServerTime = DateTime.ParseExact(generalData["time"], "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

            int timeOffsetSeconds = (int)(wikiServerTime - DateTime.UtcNow).TotalSeconds - 2;
            // 2 seconds are substracted so we never get time in the future on the server

            CultureInfo langCulture;
            // Select general and regional CultureInfo, mainly for datetime parsing
            try
            {
                langCulture = new CultureInfo(language, false);
            }
            catch (Exception)
            {
                langCulture = new CultureInfo(String.Empty);
            }


            CultureInfo regCulture = null;

            if (langCulture.Equals(CultureInfo.CurrentUICulture.Parent))
            {
                regCulture = CultureInfo.CurrentUICulture;
            }
            else
            {
                try
                {
                    regCulture = CultureInfo.CreateSpecificCulture(language);
                }
                catch (Exception)
                {
                    foreach (CultureInfo ci in
                        CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                    {
                        if (langCulture.Equals(ci.Parent))
                        {
                            regCulture = ci;
                            break;
                        }
                    }
                    if (regCulture == null)
                    {
                        regCulture = CultureInfo.InvariantCulture;
                    }
                }
            }

            // Load local redirection tags
            generalData["redirectTags"] = (
                from el in WikiParser.CommonDataXml.Element("RedirectionTags").Descendants("rd")
                where el.Attribute("lang").Value == language
                select el.Value
            ).SingleOrDefault();


            return new SiteInformation
            {
                Capitalization = capitalization,
                LangCulture = langCulture,
                Language = language,
                Name = name,
                Namespaces = wikiNamespaceDictionary,
                RegCulture = regCulture,
                ShortPath = shortPath,
                Software = software,
                TimeOffset = timeOffset,
                Version = version,
                TimeOffsetSeconds = timeOffsetSeconds,
                Regexes = new RegexSet
                {
                    Redirect = new Regex(@"(?i)^ *#(?:" + generalData["redirectTags"] + @")\s*:?\s*\[\[(.+?)(\|.+)?]]", RegexOptions.Compiled),
                    MagicWordsAndVars = new Regex(@"^(?:" + generalData["magicWords"].ToLower() + '|' + generalData["variables"] + ')', RegexOptions.Compiled),
                    AllNsPrefixes = new Regex(@"(?i)^(?:" + wikiNamespaceDictionary.GetAllNsPrefixes() + "):", RegexOptions.Compiled),
                    InterwikiLink = new Regex(@"(?i)\[\[((" + generalData["interwiki"] + "):(.+?))]]", RegexOptions.Compiled),
                    WikiCategory = new Regex(@"(?i)\[\[\s*(((" + wikiNamespaceDictionary.GetNsPrefixes(14) + @"):(.+?))(\|.+?)?)]]", RegexOptions.Compiled),
                    WikiImage = new Regex(@"\[\[(?i)((" + wikiNamespaceDictionary.GetNsPrefixes(6) + @"):(.+?))(\|(.+?))*?]]", RegexOptions.Compiled),
                    LinkToImage2 = new Regex("<a href=\"[^\"]*?\" title=\"(" + Regex.Escape(wikiNamespaceDictionary.GetNsPrefix(6)) + "[^\"]+?)\">", RegexOptions.Compiled),
                    TitleLink = new Regex("<a [^>]*title=\"(?<title>.+?)\"", RegexOptions.Compiled),
                    TitleLinkInList = new Regex("<li(?: [^>]*)?>\\s*<a [^>]*title=\"(?<title>.+?)\"", RegexOptions.Compiled),
                    TitleLinkInTable = new Regex("<td(?: [^>]*)?>\\s*<a [^>]*title=\"(?<title>.+?)\"", RegexOptions.Compiled),
                    TitleLinkShown = new Regex("<a [^>]*title=\"([^\"]+)\"[^>]*>\\s*\\1\\s*</a>", RegexOptions.Compiled),
                    LinkToSubCategory = new Regex(">([^<]+)</a></div>\\s*<div class=\"CategoryTreeChildren\"", RegexOptions.Compiled),
                    LinkToImage = new Regex("<div class=\"gallerytext\">\n<a href=\"[^\"]*?\" title=\"([^\"]+?)\">", RegexOptions.Compiled),
                    WikiLink = new Regex(@"\[\[(?<link>(?<title>.+?)(?<params>\|.+?)?)]]", RegexOptions.Compiled),
                    WikiTemplate = new Regex(@"(?s)\{\{(.+?)((\|.*?)*?)}}", RegexOptions.Compiled),
                    WebLink = new Regex("(https?|t?ftp|news|nntp|telnet|irc|gopher)://([^\\s'\"<>]+)", RegexOptions.Compiled),

                    NoWikiMarkup = new Regex("(?is)<nowiki>(.*?)</nowiki>", RegexOptions.Compiled),

                    EditToken = new Regex("(?i)value=\"([^\"]+)\"[^>]+name=\"wpEditToken\"" + "|name=\"wpEditToken\"[^>]+value=\"([^\"]+)\"", RegexOptions.Compiled),
                    EditTime = new Regex("(?i)value=\"([^\"]+)\"[^>]+name=\"wpEdittime\"" + "|name=\"wpEdittime\"[^>]+value=\"([^\"]+)\"", RegexOptions.Compiled),
                    StartTime = new Regex("(?i)value=\"([^\"]+)\"[^>]+name=\"wpStarttime\"" + "|name=\"wpStarttime\"[^>]+value=\"([^\"]+)\"", RegexOptions.Compiled),
                    BaseRevId = new Regex("(?i)value=\"([^\"]+)\"[^>]+name=\"baseRevId\"" + "|name=\"baseRevId\"[^>]+value=\"([^\"]+)\"", RegexOptions.Compiled)
                },
            };
        }
        
        public static Page DeserializePage(string src)
        {
            XElement pageXml = XElement.Parse(src).Element("query").Element("pages").Element("page");

            if (pageXml.Attribute("missing") != null)
            {
                return null;
            }

            XElement revXml = pageXml.Element("revisions").Element("rev");
            long.TryParse(revXml.Attribute("revid").Value, out long revisionId);


            return new Page
            {
                LastLoadTime = DateTime.UtcNow,
                PageId = pageXml.Attribute("pageid").Value,                
                LastUserId = revXml.Attribute("userid").Value,
                LastMinorEdit = revXml.Attribute("minor") != null,

                Content = new PageContent
                {
                    Text = revXml.Value,
                    Title = pageXml.Attribute("title").Value,
                },               

                Revision = new Revision
                {
                    Id = revisionId,
                    Timestamp = DateTime.Parse(revXml.Attribute("timestamp").Value).ToUniversalTime(),
                    LastUser = revXml.Attribute("user").Value,
                    Comment = revXml.Attribute("comment").Value,
                }
            };
        }
    }
}
