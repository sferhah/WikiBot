using System;
using System.Collections.Generic;

namespace WikiBot
{
    public static class TemplateHelper
    {
        /// <summary>Parses the provided template body and returns the key/value pairs of it's
        /// parameters titles and values. Everything inside the double braces must be passed to
        /// this function, so first goes the template's title, then '|' character, and then go the
        /// parameters. Please, see the usage example.</summary>
        /// <param name="template">Complete template's body including it's title, but not
        /// including double braces.</param>
        /// <returns>Returns the Dictionary &lt;string, string&gt; object, where keys are parameters
        /// titles and values are parameters values. If parameter is untitled, it's number is
        /// returned as the (string) dictionary key. If parameter value is set several times in the
        /// template (normally that shouldn't occur), only the last value is returned. Template's
        /// title is not returned as a parameter.</returns>
        /// <example><code>
        /// Dictionary &lt;string, string&gt; parameters1 =
        /// 	site.ParseTemplate("TemplateTitle|param1=val1|param2=val2");
        /// string[] templates = page.GetTemplates(true, false);
        /// Dictionary &lt;string, string&gt; parameters2 = site.ParseTemplate(templates[0]);
        /// parameters1["param2"] = "newValue";
        /// </code></example>
        public static Dictionary<string, string> ParseTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new ArgumentNullException("template");
            }

            if (template.StartsWith("{{"))
            {
                template = template.Substring(2, template.Length - 4);
            }

            int startPos, endPos, len = 0;
            string str = template;

            while ((startPos = str.LastIndexOf("{{")) != -1)
            {
                endPos = str.IndexOf("}}", startPos);
                len = (endPos != -1) ? endPos - startPos + 2 : 2;
                str = str.Remove(startPos, len);
                str = str.Insert(startPos, new String('_', len));
            }

            while ((startPos = str.LastIndexOf("[[")) != -1)
            {
                endPos = str.IndexOf("]]", startPos);
                len = (endPos != -1) ? endPos - startPos + 2 : 2;
                str = str.Remove(startPos, len);
                str = str.Insert(startPos, new String('_', len));
            }

            List<int> separators = str.GetMatchesPositions("|", false);
            if (separators == null || separators.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            List<string> parameters = new List<string>();
            endPos = template.Length;

            for (int i = separators.Count - 1; i >= 0; i--)
            {
                parameters.Add(template.Substring(separators[i] + 1, endPos - separators[i] - 1));
                endPos = separators[i];
            }

            parameters.Reverse();

            Dictionary<string, string> templateParams = new Dictionary<string, string>();
            for (int pos, i = 0; i < parameters.Count; i++)
            {
                pos = parameters[i].IndexOf('=');
                if (pos == -1)
                {
                    templateParams[i.ToString()] = parameters[i].Trim();
                }
                else
                {
                    templateParams[parameters[i].Substring(0, pos).Trim()] = parameters[i].Substring(pos + 1).Trim();
                }
            }

            return templateParams;
        }

        /// <summary>Formats a template with the specified title and parameters. Default formatting
        /// options are used.</summary>
        /// <param name="templateTitle">Template's title.</param>
        /// <param name="templateParams">Template's parameters in Dictionary &lt;string, string&gt;
        /// object, where keys are parameters titles and values are parameters values.</param>
        /// <returns>Returns the complete template in double braces.</returns>
        public static string FormatTemplate(string templateTitle, Dictionary<string, string> templateParams)
        {
            return FormatTemplate(templateTitle, templateParams, false, false, 0);
        }

        /// <summary>Formats a template with the specified title and parameters. Formatting
        /// options are got from provided reference template. That function is usually used to
        /// format modified template as it was in it's initial state, though absolute format
        /// consistency can not be guaranteed.</summary>
        /// <param name="templateTitle">Template's title.</param>
        /// <param name="templateParams">Template's parameters in Dictionary &lt;string, string&gt;
        /// object, where keys are parameters titles and values are parameters values.</param>
        /// <param name="referenceTemplate">Full template body to detect formatting options in.
        /// With or without double braces.</param>
        /// <returns>Returns the complete template in double braces.</returns>
        public static string FormatTemplate(string templateTitle, Dictionary<string, string> templateParams, string referenceTemplate)
        {
            if (string.IsNullOrEmpty(referenceTemplate))
                throw new ArgumentNullException("referenceTemplate");

            bool inline = false;
            bool withoutSpaces = false;
            int padding = 0;

            if (!referenceTemplate.Contains("\n|") && !referenceTemplate.Contains("\n |"))
                inline = true;
            if (!referenceTemplate.Contains("| ") && !referenceTemplate.Contains("= "))
                withoutSpaces = true;
            if (!inline && referenceTemplate.Contains("  ="))
                padding = -1;

            return FormatTemplate(templateTitle, templateParams, inline, withoutSpaces, padding);
        }

        /// <summary>Formats a template with the specified title and parameters, allows extended
        /// format options to be specified.</summary>
        /// <param name="templateTitle">Template's title.</param>
        /// <param name="templateParams">Template's parameters in Dictionary &lt;string, string&gt;
        /// object, where keys are parameters titles and values are parameters values.</param>
        /// <param name="inline">When set to true, template is formatted in one line, without any
        /// line breaks. Default value is false.</param>
        /// <param name="withoutSpaces">When set to true, template is formatted without spaces.
        /// Default value is false.</param>
        /// <param name="padding">When set to positive value, template parameters titles are padded
        /// on the right with specified number of spaces, so "=" characters could form a nice
        /// straight column. When set to -1, the number of spaces is calculated automatically.
        /// Default value is 0 (no padding). The padding will occur only when "inline" option
        /// is set to false and "withoutSpaces" option is also set to false.</param>
        /// <returns>Returns the complete template in double braces.</returns>
        public static string FormatTemplate(string templateTitle, Dictionary<string, string> templateParams, bool inline, bool withoutSpaces, int padding)
        {
            if (string.IsNullOrEmpty(templateTitle))
                throw new ArgumentNullException("templateTitle");
            if (templateParams == null || templateParams.Count == 0)
                throw new ArgumentNullException("templateParams");

            if (inline != false || withoutSpaces != false)
            {
                padding = 0;
            }

            if (padding == -1)
            {
                foreach (KeyValuePair<string, string> kvp in templateParams)
                {
                    if (kvp.Key.Length > padding)
                    {
                        padding = kvp.Key.Length;
                    }
                }
            }

            string paramBreak = "|";
            string equalsSign = "=";

            if (!inline)
            {
                paramBreak = "\n|";
            }

            if (!withoutSpaces)
            {
                equalsSign = " = ";
                paramBreak += " ";
            }

            int i = 1;
            string template = "{{" + templateTitle;

            foreach (KeyValuePair<string, string> kvp in templateParams)
            {
                template += paramBreak;
                if (padding <= 0)
                {
                    if (kvp.Key == i.ToString())
                        template += kvp.Value;
                    else
                        template += kvp.Key + equalsSign + kvp.Value;
                }
                else
                {
                    if (kvp.Key == i.ToString())
                        template += kvp.Value.PadRight(padding + 3);
                    else
                        template += kvp.Key.PadRight(padding) + equalsSign + kvp.Value;
                }
                i++;
            }

            if (!inline)
            {
                template += "\n";
            }

            template += "}}";

            return template;
        }
    }
}
