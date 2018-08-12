using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace WikiBot
{   
    public class WikiNamespaceDictionary
    {
        /// <summary>Local namespaces, default namespaces and local namespace aliases, joined into
        /// strings, enclosed in and delimited by '|' character.</summary>
        private Dictionary<int, string> namespaces;

        public WikiNamespaceDictionary(Dictionary<int, string> namespaces) 
            => this.namespaces = namespaces ?? new Dictionary<int, string>();

        public string GetAllNsPrefixes()
            => string.Join("|", namespaces.Select(x => x.Value.Substring(1, x.Value.Length - 2)).ToArray()).Replace("||", "|");

        /// <summary>Gets main local prefix for specified namespace and colon.</summary>
        /// <param name="nsIndex">Index of namespace to get prefix for.</param>
        /// <returns>Returns the prefix with colon, e.g., "Kategorie:".</returns>
        public string GetNsPrefix(int nsIndex)
        {
            if (nsIndex == 0)
            {
                return String.Empty;
            }

            if (!namespaces.Keys.Contains(nsIndex))
            {
                throw new ArgumentOutOfRangeException("nsIndex");
            }

            return namespaces[nsIndex].Substring(1, namespaces[nsIndex].IndexOf('|', 1) - 1) + ':';
        }

        /// <summary>Gets canonical default English prefix for specified namespace and colon.
        /// If default prefix is not found the main local prefix is returned.</summary>
        /// <param name="nsIndex">Index of namespace to get prefix for.</param>
        /// <returns>Returns the prefix with colon, e.g., "Category:".</returns>
        public string GetEnglishNsPrefix(int nsIndex)
        {
            if (nsIndex == 0)
            {
                return String.Empty;
            }

            if (!namespaces.Keys.Contains(nsIndex))
            {
                throw new ArgumentOutOfRangeException("nsIndex");
            }

            int secondDelimPos = namespaces[nsIndex].IndexOf('|', 1);
            int thirdDelimPos = namespaces[nsIndex].IndexOf('|', secondDelimPos + 1);

            if (thirdDelimPos == -1)
            {
                return namespaces[nsIndex].Substring(1, secondDelimPos - 1) + ':';
            }
            else
            {
                return namespaces[nsIndex].Substring(secondDelimPos + 1, thirdDelimPos - secondDelimPos - 1) + ':';
            }
        }

        /// <summary>Gets all names and aliases for specified namespace delimited by '|' character
        /// and escaped for use within Regex patterns.</summary>
        /// <param name="nsIndex">Index of namespace to get prefixes for.</param>
        /// <returns>Returns prefixes string, e.g. "Category|Kategorie".</returns>
        public string GetNsPrefixes(int nsIndex)
        {
            if (!namespaces.Keys.Contains(nsIndex))
            {
                throw new ArgumentOutOfRangeException("nsIndex");
            }

            string str = namespaces[nsIndex].Substring(1, namespaces[nsIndex].Length - 2);
            str = str.Replace('|', '%');    // '%' is not escaped
            str = Regex.Escape(str);    // escapes only \*+?|{[()^$.# and whitespaces
            str = str.Replace('%', '|');    // return back '|' delimeter
            return str;
        }

        /// <summary>Identifies the namespace of the page.</summary>
        /// <param name="pageTitle">Page title to identify the namespace of.</param>
        /// <returns>Returns the integer key of the namespace.</returns>
        public int GetNamespace(string pageTitle)
        {
            if (string.IsNullOrEmpty(pageTitle))
            {
                throw new ArgumentNullException("pageTitle");
            }

            int colonPos = pageTitle.IndexOf(':');

            if (colonPos == -1 || colonPos == 0)
            {
                return 0;
            }

            string pageNS = '|' + pageTitle.Substring(0, colonPos) + '|';

            foreach (KeyValuePair<int, string> ns in namespaces)
            {
                if (ns.Value.Contains(pageNS))
                    return ns.Key;
            }

            return 0;
        }

        /// <summary>Removes the namespace prefix from page title.</summary>
        /// <param name="pageTitle">Page title to remove prefix from.</param>
        /// <param name="nsIndex">Integer key of namespace to remove. If this parameter is 0
        /// any found namespace prefix is removed.</param>
        /// <returns>Page title without prefix.</returns>
        public string RemoveNsPrefix(string pageTitle, int nsIndex)
        {
            if (string.IsNullOrEmpty(pageTitle))
            {
                throw new ArgumentNullException("pageTitle");
            }

            if (!namespaces.Keys.Contains(nsIndex))
            {
                throw new ArgumentOutOfRangeException("nsIndex");
            }

            if (pageTitle[0] == ':')
            {
                pageTitle = pageTitle.TrimStart(new char[] { ':' });
            }

            int colonPos = pageTitle.IndexOf(':');

            if (colonPos == -1)
            {
                return pageTitle;
            }

            string pagePrefixPattern = '|' + pageTitle.Substring(0, colonPos) + '|';

            if (nsIndex != 0)
            {
                if (namespaces[nsIndex].Contains(pagePrefixPattern))
                {
                    return pageTitle.Substring(colonPos + 1);
                }
            }
            else
            {
                foreach (KeyValuePair<int, string> ns in namespaces)
                {
                    if (ns.Value.Contains(pagePrefixPattern))
                    {
                        return pageTitle.Substring(colonPos + 1);
                    }
                }
            }

            return pageTitle;
        }

        /// <summary>Function changes default English namespace prefixes and local namespace aliases
        /// to canonical local prefixes (e.g. for German wiki-sites it changes "Category:..."
        /// to "Kategorie:...").</summary>
        /// <param name="pageTitle">Page title to correct prefix in.</param>
        /// <returns>Page title with corrected prefix.</returns>
        public string CorrectNsPrefix(string pageTitle)
        {
            if (string.IsNullOrEmpty(pageTitle))
            {
                throw new ArgumentNullException("pageTitle");
            }

            if (pageTitle[0] == ':')
            {
                pageTitle = pageTitle.TrimStart(new char[] { ':' });
            }

            int ns = GetNamespace(pageTitle);

            if (ns == 0)
            {
                return pageTitle;
            }

            return GetNsPrefix(ns) + RemoveNsPrefix(pageTitle, ns);
        }
    }

}
