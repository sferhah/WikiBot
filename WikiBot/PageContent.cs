using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace WikiBot
{
    /// <summary>Class defines wiki page object.</summary> 
    public class PageContent
    {

        internal PageContent() { }   

        /// <summary>Page's title, including namespace prefix.</summary>
        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                _title = value.FormatTitle();
            }
        }

        private string _text;
        /// <summary>Page's text.</summary>
        public string Text
        {
            get => _text;
            set
            {
                if (IsReadOnly)
                {
                    throw new BotDisallowedException(string.Format(
                        "Bot action on \"{0}\" page is prohibited " +
                        "by \"nobots\" or \"bots|allow=none\" template.", this.Title));
                }

                _text = value;
            }
        }

        public bool IsReadOnly { get; internal set; }

        public override string ToString() => this.Text;


        /// <summary>Function converts basic HTML markup in this page's text to wiki
        /// markup, except for tables markup. Use
        /// <see cref="ConvertHtmlTablesToWikiTables()"/> function to convert HTML
        /// tables markup to wiki format.</summary>
        public void ConvertHtmlMarkupToWikiMarkup()
        {
            Text = Regex.Replace(Text, "(?is)n?<(h1)( [^/>]+?)?>(.+?)</\\1>n?", "\n= $3 =\n");
            Text = Regex.Replace(Text, "(?is)n?<(h2)( [^/>]+?)?>(.+?)</\\1>n?", "\n== $3 ==\n");
            Text = Regex.Replace(Text, "(?is)n?<(h3)( [^/>]+?)?>(.+?)</\\1>n?", "\n=== $3 ===\n");
            Text = Regex.Replace(Text, "(?is)n?<(h4)( [^/>]+?)?>(.+?)</\\1>n?", "\n==== $3 ====\n");
            Text = Regex.Replace(Text, "(?is)n?<(h5)( [^/>]+?)?>(.+?)</\\1>n?", "\n===== $3 =====\n");
            Text = Regex.Replace(Text, "(?is)n?<(h6)( [^/>]+?)?>(.+?)</\\1>n?", "\n====== $3 ======\n");
            Text = Regex.Replace(Text, "(?is)\n?\n?<p( [^/>]+?)?>(.+?)</p>", "\n\n$2");
            Text = Regex.Replace(Text, "(?is)<a href ?= ?[\"'](http:[^\"']+)[\"']>(.+?)</a>", "[$1 $2]");
            Text = Regex.Replace(Text, "(?i)</?(b|strong)>", "'''");
            Text = Regex.Replace(Text, "(?i)</?(i|em)>", "''");
            Text = Regex.Replace(Text, "(?i)\n?<hr ?/?>\n?", "\n----\n");
            Text = Regex.Replace(Text, "(?i)<(hr|br)( [^/>]+?)? ?/?>", "<$1$2 />");
        }

        /// <summary>Function converts HTML table markup in this page's text to wiki
        /// table markup.</summary>
        /// <seealso cref="ConvertHtmlMarkupToWikiMarkup()"/>
        public void ConvertHtmlTablesToWikiTables()
        {
            if (!Text.Contains("</table>"))
            {
                return;
            }

            Text = Regex.Replace(Text, ">\\s+<", "><");
            Text = Regex.Replace(Text, "<table( ?[^>]*)>", "\n{|$1\n");
            Text = Regex.Replace(Text, "</table>", "|}\n");
            Text = Regex.Replace(Text, "<caption( ?[^>]*)>", "|+$1 | ");
            Text = Regex.Replace(Text, "</caption>", "\n");
            Text = Regex.Replace(Text, "<tr( ?[^>]*)>", "|-$1\n");
            Text = Regex.Replace(Text, "</tr>", "\n");
            Text = Regex.Replace(Text, "<th([^>]*)>", "!$1 | ");
            Text = Regex.Replace(Text, "</th>", "\n");
            Text = Regex.Replace(Text, "<td([^>]*)>", "|$1 | ");
            Text = Regex.Replace(Text, "</td>", "\n");
            Text = Regex.Replace(Text, "\n(\\||\\|\\+|!) \\| ", "\n$1 ");
            Text = Text.Replace("\n\n|", "\n|");
        }

        /// <summary>Local namespaces, default namespaces and local namespace aliases, joined into
        /// strings, enclosed in and delimited by '|' character.</summary>
        internal WikiNamespaceDictionary Namespaces { get; set; }

        /// <summary>A set of regular expressions for parsing pages. Usually there is no need
        /// to edit these regular expressions manually.</summary>
        internal RegexSet Regexes { get; set; }

        /// <summary>A set of regular expressions for parsing pages. Usually there is no need
        /// to edit these regular expressions manually.</summary>
        internal string DisambigExpression { get; set; }

        /// <summary>Identifies the namespace of the page.</summary>
        /// <returns>Returns the integer key of the namespace.</returns>
        public int Namespace => this.Namespaces.GetNamespace(this.Title);

        /// <summary>Returns true, if page redirects to another page. Don't forget to load
        /// actual page contents from live wiki <see cref="Page.Load()"/> before using this
        /// function.</summary>
        /// <returns>Returns bool value.</returns>
        public bool IsRedirect => (string.IsNullOrEmpty(this.Text)) ? false : this.Regexes.Redirect.IsMatch(this.Text);

        /// <summary>Returns true, if this page is a disambiguation page. This function
        /// automatically recognizes disambiguation templates on Wikipedia sites in
        /// different languages. But in order to be used on other sites, <see cref="Bot.disambig"/>
        /// variable must be manually set before this function is called.
        /// <see cref="Bot.disambig"/> should contain local disambiguation template's title or
        /// several titles delimited by '|' character, letters case doesn't matter, e.g.
        /// "disambiguation|disambig|disam". Page text
        /// will be loaded from wiki if it was not loaded prior to function call.</summary>
        /// <returns>Returns bool value.</returns>
        public bool IsDisambig => Regex.IsMatch(this.Text ?? String.Empty, @"(?i)\{\{(" + this.DisambigExpression + ")}}");

        /// <summary>Returns a list of files, embedded in this page.</summary>
        /// <returns>Returns the List object. Returned file names contain namespace prefixes.
        /// The list can be empty. Strings in list may recur
        /// indicating that file was embedded several times.</returns>
        public List<string> Images
        {
            get
            {
                if (string.IsNullOrEmpty(this.Text))
                {
                    throw new ArgumentNullException("text");
                }

                string nsPrefixes = this.Namespaces.GetNsPrefixes(6);
                
                List<string> matchStrings = new List<string>();

                foreach (Match m in Regex.Matches(this.Text, @"\[\[\s*((?i)" + nsPrefixes + @"):(?<filename>[^|\]]+)"))
                {
                    matchStrings.Add(this.Namespaces.GetNsPrefix(6) + m.Groups["filename"].Value.Trim());
                }

                if (Regex.IsMatch(this.Text, "(?i)<gallery>"))
                {   
                    foreach (Match m in Regex.Matches(this.Text, @"^\s*((?i)" + nsPrefixes + "):(?<filename>[^|\\]\r?\n]+)"))
                    {
                        matchStrings.Add(this.Namespaces.GetNsPrefix(6) + m.Groups["filename"].Value.Trim());
                    }
                }

                return matchStrings;
            }
        }


        /// <summary>Returns the list of strings which contains external links
        /// found in page's text.</summary>
        /// <returns>Returns the List object.</returns>
        public IEnumerable<string> ExternalLinks 
            => this.Regexes.WebLink.Matches(this.Text).Cast<Match>().Select(m => m.Value);

        /// <summary>Returns the list of strings, containing all wikilinks ([[...]])
        /// found in page's text, excluding links in image descriptions, but including
        /// interlanguage links, links to sister wiki projects, categories, images, etc.</summary>
        /// <returns>Returns untouched links in a List.</returns>
        public IEnumerable<string> AllLinks
            => this.Regexes.WikiLink.Matches(this.Text).Cast<Match>().Select(m => m.Groups["title"].Value.Trim());


        /// <summary>Returns redirection target. Don't forget to load
        /// actual page contents from live wiki <see cref="Page.Load()"/> before using this
        /// function.</summary>
        /// <returns>Returns redirection target page title. Returns empty string, if this
        /// Page is not a redirection.</returns>
        public string RedirectsTo 
            => IsRedirect ? this.Regexes.Redirect.Match(this.Text).Groups[1].ToString().Trim() : string.Empty;

        /// <summary>Changes default English namespace prefixes to correct local prefixes
        /// (e.g. for German wiki sites it changes "Category:..." to "Kategorie:...").</summary>
        public void CorrectPageNsPrefix()
            => this.Title = this.Namespaces.CorrectNsPrefix(this.Title);



        /// <summary>Adds the page to the specified category by adding a
        /// link to that category to the very end of page's text.
        /// If the link to the specified category
        /// already exists, the function silently does nothing.</summary>
        /// <param name="categoryName">Category name, with or without prefix.
        /// Sort key can also be included after "|", like "Category:Stars|D3".</param>
        public void AddToCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException("categoryName");
            }

            categoryName = this.Namespaces.RemoveNsPrefix(categoryName, 14);

            string cleanCategoryName = !categoryName.Contains("|") ? categoryName.Trim() : categoryName.Substring(0, categoryName.IndexOf('|')).Trim();

            List<string> categories = GetCategories(false, false);

            foreach (string category in categories)
            {
                if (category == cleanCategoryName.Capitalize()
                    || category == cleanCategoryName.Uncapitalize())
                {
                    return;
                }
            }

            this.Text += (categories.Count == 0 ? "\n" : String.Empty) + "\n[[" + this.Namespaces.GetNsPrefix(14) + categoryName + "]]\n";
            this.Text = this.Text.TrimEnd("\r\n".ToCharArray());
        }


        /// <summary>Removes the page from category by deleting link to that category in
        /// page text.</summary>
        /// <param name="categoryName">Category name, with or without prefix.</param>
        public void RemoveFromCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException("categoryName");
            }

            categoryName = this.Namespaces.RemoveNsPrefix(categoryName, 14).Trim();
            categoryName = !categoryName.Contains("|") ? categoryName : categoryName.Substring(0, categoryName.IndexOf('|'));

            List<string> categories = GetCategories(false, false);

            if (!categories.Contains(categoryName.Capitalize())
                && !categories.Contains(categoryName.Uncapitalize()))
            {
                return;
            }

            string regexCategoryName = Regex.Escape(categoryName);
            regexCategoryName = regexCategoryName.Replace("_", "\\ ").Replace("\\ ", "[_\\ ]");

            int firstCharIndex = (regexCategoryName[0] == '\\') ? 1 : 0;

            regexCategoryName = "[" + char.ToLower(regexCategoryName[firstCharIndex]) + char.ToUpper(regexCategoryName[firstCharIndex]) + "]" + regexCategoryName.Substring(firstCharIndex + 1);

            this.Text = Regex.Replace(this.Text, @"\[\[((?i)" + this.Namespaces.GetNsPrefixes(14) + "): ?" + regexCategoryName + @"(\|.*?)?]]\r?\n?", String.Empty);
            this.Text = this.Text.TrimEnd("\r\n".ToCharArray());
        }


        /// <summary>Returns the list of strings which contains category names found in
        /// page's text, with namespace prefix, without sorting keys. You can use the resultant
        /// strings to call <see cref="PageList.FillFromCategory(string)"/>
        /// or <see cref="PageList.FillFromCategoryTree(string)"/>
        /// function. Categories added by templates are not returned. Use GetAllCategories()
        /// function to get such categories too.</summary>
        /// <returns>Returns the List object.</returns>
        public List<string> Categories => GetCategories(true, false);

        /// <summary>Returns the list of strings which contains categories' names found in
        /// page text. Categories added by templates are not returned. Use
        /// <see cref="Page.GetAllCategories()"/>
        /// function to get categories added by templates too.</summary>
        /// <param name="withNameSpacePrefix">If true, function returns strings with
        /// namespace prefix like "Category:Stars", not just "Stars".</param>
        /// <param name="withSortKey">If true, function returns strings with sort keys,
        /// if found. Like "Stars|D3" (in [[Category:Stars|D3]]).</param>
        /// <returns>Returns the List object.</returns>
        public List<string> GetCategories(bool withNameSpacePrefix, bool withSortKey)
        {
            List<string> matchStrings = new List<string>();

            foreach (Match m in this.Regexes.WikiCategory.Matches(Regex.Replace(this.Text, "(?is)<nowiki>.+?</nowiki>", String.Empty)))
            {
                string str = m.Groups[4].Value.Trim();

                if (withSortKey)
                {
                    str += m.Groups[5].Value.Trim();
                }

                if (withNameSpacePrefix)
                {
                    str = this.Namespaces.GetNsPrefix(14) + str;
                }

                matchStrings.Add(str);
            }

            return matchStrings;
        }




        /// <summary>Adds a specified template to the end of the page text,
        /// but before categories.</summary>
        /// <param name="templateText">Complete template in double brackets,
        /// e.g. "{{TemplateTitle|param1=val1|param2=val2}}".</param>
        public void AddTemplate(string templateText)
        {
            if (string.IsNullOrEmpty(templateText))
            {
                throw new ArgumentNullException("templateText");
            }

            Regex templateInsertion = new Regex("([^}]\n|}})\n*\\[\\[((?i)" + this.Namespaces.GetNsPrefixes(14) + "):");

            if (templateInsertion.IsMatch(this.Text))
            {
                this.Text = templateInsertion.Replace(this.Text, "$1\n" + templateText + "\n\n[[" + this.Namespaces.GetNsPrefix(14), 1);
            }
            else
            {
                this.Text += "\n\n" + templateText;
                this.Text = this.Text.TrimEnd("\r\n".ToCharArray());
            }
        }




        /// <summary>Removes all instances of a specified template from page text.</summary>
        /// <param name="templateTitle">Title of template to remove.</param>
        public void RemoveTemplate(string templateTitle)
        {
            if (string.IsNullOrEmpty(templateTitle))
            {
                throw new ArgumentNullException("templateTitle");
            }

            templateTitle = Regex.Escape(templateTitle);
            templateTitle = "(" + Char.ToUpper(templateTitle[0]) + "|" + Char.ToLower(templateTitle[0]) + ")" + (templateTitle.Length > 1 ? templateTitle.Substring(1) : String.Empty);
            this.Text = Regex.Replace(this.Text, @"(?s)\{\{\s*" + templateTitle + @"(.*?)}}\r?\n?", String.Empty);
        }



        /// <summary>This helper method returns specified parameter of a first found instance of
        /// specified template. If no such template or no such parameter was found,
        /// empty string "" is returned.</summary>
        /// <param name="templateTitle">Title of template to get parameter of.</param>
        /// <param name="templateParameter">Title of template's parameter. If parameter is
        /// untitled, specify it's number as string. If parameter is titled, but it's number is
        /// specified, the function will return empty List &lt;string&gt; object.</param>
        /// <returns>Returns parameter as string or empty string "".</returns>
        /// <remarks>Thanks to Eyal Hertzog and metacafe.com team for idea of this
        /// function.</remarks>
        public string GetFirstTemplateParameter(string templateTitle, string templateParameter)
            => GetTemplateParameter(templateTitle, templateParameter).FirstOrDefault() ?? String.Empty;

        /// <summary>Returns the templates found in text of this page (those inside double
        /// curly brackets {{...}} ). MediaWiki's
        /// <see href="https://www.mediawiki.org/wiki/Help:Magic_words">"magic words"</see>
        /// are not returned as templates.</summary>
        /// <param name="withParameters">If set to true, everything inside curly brackets is
        /// returned with all parameters untouched.</param>
        /// <param name="includePages">If set to true, titles of "transcluded" pages are returned as
        /// templates and messages with "msgnw:" prefix are also returned as templates. See
        /// <see href="https://www.mediawiki.org/wiki/Transclusion">this page</see> for details.
        /// Default is false.</param>
        /// <returns>Returns the List object.</returns>
        public List<string> GetTemplates(bool withParameters, bool includePages)
        {
            // Blank unsuitable regions with '_' char for easiness
            string str = this.Regexes.NoWikiMarkup.Replace(this.Text, match => new string('_', match.Value.Length));

            if (this.Namespace == 10)    // template
            {
                str = Regex.Replace(str, @"\{\{\{.*?}}}", match => new string('_', match.Value.Length));  // remove template parameters
            }

            Dictionary<int, int> templPos = new Dictionary<int, int>();
            List<string> templates = new List<string>();
            int startPos, endPos, len = 0;
            // Find all templates positions, blank found templates with '_' char for easiness
            while ((startPos = str.LastIndexOf("{{")) != -1)
            {
                endPos = str.IndexOf("}}", startPos);
                len = (endPos != -1) ? endPos - startPos + 2 : 2;

                if (len != 2)
                {
                    templPos.Add(startPos, len);
                }

                str = str.Remove(startPos, len);
                str = str.Insert(startPos, new String('_', len));
            }

            // Collect templates using found positions, remove non-templates
            foreach (KeyValuePair<int, int> pos in templPos)
            {
                str = this.Text.Substring(pos.Key + 2, pos.Value - 4).Trim();

                if (str == String.Empty || str[0] == '#')
                {
                    continue;
                }

                if (this.Regexes.MagicWordsAndVars.IsMatch(str))
                {
                    continue;
                }

                if (!withParameters)
                {
                    endPos = str.IndexOf('|');

                    if (endPos != -1)
                    {
                        str = str.Substring(0, endPos);
                    }

                    if (str == String.Empty)
                    {
                        continue;
                    }
                }

                if (!includePages)
                {
                    if (str[0] == ':'
                        || this.Regexes.AllNsPrefixes.IsMatch(str)
                        || str.StartsWith("msgnw:")
                        || str.StartsWith("MSGNW:"))
                        continue;
                }
                else
                {
                    if (str[0] == ':')
                        str = str.Remove(0, 1);
                    else if (str.StartsWith("msgnw:") || str.StartsWith("MSGNW:"))
                        str = str.Remove(0, 6);
                }

                templates.Add(str);
            }

            templates.Reverse();
            return templates;
        }


        /// <summary>Returns specified parameter of a specified template. If several instances
        /// of specified template are found in text of this page, all parameter values
        /// are returned.</summary>
        /// <param name="templateTitle">Title of template to get parameter of.</param>
        /// <param name="templateParameter">Title of template's parameter. If parameter is
        /// untitled, specify it's number as string. If parameter is titled, but it's number is
        /// specified, the function will return empty List &lt;string&gt; object.</param>
        /// <returns>Returns the List &lt;string&gt; object with strings, containing values of
        /// specified parameters in all found template instances. Returns empty List &lt;string&gt;
        /// object if no specified template parameters were found.</returns>
        public List<string> GetTemplateParameter(string templateTitle, string templateParameter)
        {
            if (string.IsNullOrEmpty(templateTitle))
                throw new ArgumentNullException("templateTitle");
            if (string.IsNullOrEmpty(templateParameter))
                throw new ArgumentNullException("templateParameter");
            if (string.IsNullOrEmpty(this.Text))
                throw new ArgumentNullException("text");

            List<string> parameterValues = new List<string>();
            templateTitle = templateTitle.Trim();
            templateParameter = templateParameter.Trim();

            Regex templateTitleRegex = new Regex("^\\s*(" +
                Regex.Escape(templateTitle).Capitalize() + "|" +
                Regex.Escape(templateTitle).Uncapitalize() +
                ")\\s*\\|");

            foreach (string template in GetTemplates(true, false))
            {
                if (templateTitleRegex.IsMatch(template))
                {
                    Dictionary<string, string> parameters = TemplateHelper.ParseTemplate(template);
                    if (parameters.ContainsKey(templateParameter))
                    {
                        parameterValues.Add(parameters[templateParameter]);
                    }
                }
            }

            return parameterValues;
        }





        /// <summary>Removes the specified parameter of the specified template.
        /// If several instances of specified template are found in text of this page, either
        /// first instance can be affected or all instances.</summary>
        /// <param name="templateTitle">Title of template.</param>
        /// <param name="templateParameter">Title of template's parameter.</param>
        /// <param name="firstTemplateOnly">When set to true, only first found template instance
        /// is modified. When set to false, all found template instances are modified.</param>
        /// <returns>Returns the number of removed instances.</returns>
        public int RemoveTemplateParameter(string templateTitle, string templateParameter, bool firstTemplateOnly) 
            => SetTemplateParameter(templateTitle, templateParameter, null, firstTemplateOnly);

        /// <summary>Sets the specified parameter of the specified template to new value.
        /// If several instances of specified template are found in text of this page, either
        /// first value can be set, or all values in all instances.</summary>
        /// <param name="templateTitle">Title of template.</param>
        /// <param name="templateParameter">Title of template's parameter.</param>
        /// <param name="newParameterValue">New value to set the parameter to.</param>
        /// <param name="firstTemplateOnly">When set to true, only first found template instance
        /// is modified. When set to false, all found template instances are modified.</param>
        /// <returns>Returns the number of modified values.</returns>
        /// <remarks>Thanks to Eyal Hertzog and metacafe.com team for idea of this
        /// function.</remarks>
        public int SetTemplateParameter(string templateTitle, string templateParameter, string newParameterValue, bool firstTemplateOnly)
        {
            if (string.IsNullOrEmpty(templateTitle))
                throw new ArgumentNullException("templateTitle");
            if (string.IsNullOrEmpty(templateParameter))
                throw new ArgumentNullException("templateParameter");
            if (string.IsNullOrEmpty(this.Text))
                throw new ArgumentNullException("text");

            int i = 0;
            Dictionary<string, string> parameters;
            templateTitle = templateTitle.Trim();
            templateParameter = templateParameter.Trim();

            Regex templateTitleRegex = new Regex("^\\s*(" +
                Regex.Escape(templateTitle).Capitalize() + "|" +
                Regex.Escape(templateTitle).Uncapitalize() +
                ")\\s*\\|");

            foreach (string template in GetTemplates(true, false).Where(x => templateTitleRegex.IsMatch(x)))
            {
                parameters = TemplateHelper.ParseTemplate(template);

                if (newParameterValue != null)
                {
                    parameters[templateParameter] = newParameterValue;
                }
                else
                {
                    parameters.Remove(templateParameter);
                }

                string newTemplate = TemplateHelper.FormatTemplate(templateTitle, parameters, template);
                Regex oldTemplate = new Regex(Regex.Escape(template));
                newTemplate = newTemplate.Substring(2, newTemplate.Length - 4);
                newTemplate = newTemplate.TrimEnd("\n".ToCharArray());
                this.Text = oldTemplate.Replace(this.Text, newTemplate, 1);
                i++;

                if (firstTemplateOnly == true)
                {
                    break;
                }
            }

            return i;
        }


        

        public List<Section> Sections
        {
            get
            {
                var titles = new Regex("(={1,5}.*)").Matches(this.Text).Cast<Match>().Select(m => m.Value).ToArray();
                string[] texts = this.Text.Split(titles, StringSplitOptions.None);

                List<Section> sections = new List<Section>()
                {
                    new Section
                    {
                        Title = "",
                        Text = texts.FirstOrDefault(),
                    }
                };

                for (int i = 1; i < texts.Length; i++)
                {
                    var title = titles[i - 1];

                    title = title.Substring(2, title.Length - 4);

                    sections.Add(new Section
                    {
                        Title = title,
                        Text = texts[i],
                    });
                }

                return sections;
            }
        }
    }

    public class Section
    {
        public string Title { get; set; }
        public string Text { get; set; }
    }

}
