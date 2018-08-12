using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WikiBot
{
    public class SiteInformation
    {
        /// <summary>Local namespaces, default namespaces and local namespace aliases, joined into
        /// strings, enclosed in and delimited by '|' character.</summary>
        public WikiNamespaceDictionary Namespaces { get; set; }

        /// <summary>Short relative path to wiki pages (if such alias is set on the server), e.g.
        /// "/wiki/". See <see href="http://www.mediawiki.org/wiki/Manual:Short URL">this page</see>
        /// for details.</summary>
        public string ShortPath { get; set; }

        /// <summary>MediaWiki version as Version object.</summary>
        public Version Version { get; set; }

        /// <summary>Page title capitalization rule on this site.
        /// On most sites capitalization rule is "first-letter".</summary>
        public string Capitalization { get; set; }

        /// <summary>Site's time offset from UTC.</summary>
        public string TimeOffset { get; set; }

        /// <summary>Site's language.</summary>
        public string Language { get; set; }

        /// <summary>Site's language culture. Required for string comparison.</summary>
        public CultureInfo LangCulture { get; set; }

        /// <summary>Randomly chosen regional culture for this site's language.
        /// Required to parse dates.</summary>
        public CultureInfo RegCulture { get; set; }

        /// <summary>Site title, e.g. "Wikipedia".</summary>
        public string Name { get; set; }

        /// <summary>Site's software identificator, e.g. "MediaWiki 1.21".</summary>
        public string Software { get; set; }

        /// <summary>A set of regular expressions for parsing pages. Usually there is no need
        /// to edit these regular expressions manually.</summary>
        public RegexSet Regexes { get; set; }

        /// <summary>Wiki server's time offset from local computer's time in seconds.
        /// Timezones difference is omitted, UTC time is compared with UTC time.</summary>
        /// <exclude/>
        public int TimeOffsetSeconds { get; set; }
    }

    public class RegexSet
    {
        public Regex Redirect { get; set; }
        public Regex MagicWordsAndVars { get; set; }
        public Regex AllNsPrefixes { get; set; }
        public Regex InterwikiLink { get; set; }
        public Regex WikiCategory { get; set; }
        public Regex WikiImage { get; set; }
        public Regex LinkToImage2 { get; set; }
        public Regex TitleLink { get; set; }
        public Regex TitleLinkInList { get; set; }
        public Regex TitleLinkInTable { get; set; }
        public Regex TitleLinkShown { get; set; }
        public Regex LinkToSubCategory { get; set; }
        public Regex LinkToImage { get; set; }
        public Regex WikiLink { get; set; }
        public Regex WikiTemplate { get; set; }
        public Regex WebLink { get; set; }
        public Regex NoWikiMarkup { get; set; }
        public Regex EditToken { get; set; }
        public Regex EditTime { get; set; }
        public Regex StartTime { get; set; }
        public Regex BaseRevId { get; set; }
    }
}
