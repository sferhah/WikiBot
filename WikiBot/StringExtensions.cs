using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace WikiBot
{
    public static class StringExtensions
    {
        public static string FormatTitle(this string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentNullException("title");
            }

            if (title[0] == ':')
            {
                title = title.TrimStart(new char[] { ':' });
            }

            if (title.Contains('_'))
            {
                title = title.Replace('_', ' ');
            }

            return title;
        }

        /// <summary>This auxiliary function makes the first letter in specified string upper-case.
        /// This is often required, but strangely there is no such function in .NET Framework's
        /// String class.</summary>
        /// <param name="str">String to capitalize.</param>
        /// <returns>Capitalized string.</returns>
        public static string Capitalize(this string str) => char.IsUpper(str[0]) ? str : char.ToUpper(str[0]) + str.Substring(1);

        /// <summary>This auxiliary function makes the first letter in specified string lower-case.
        /// This is often required, but strangely there is no such function in .NET Framework's
        /// String class.</summary>
        /// <param name="str">String to uncapitalize.</param>
        /// <returns>Returns uncapitalized string.</returns>
        public static string Uncapitalize(this string str) => char.IsLower(str[0]) ? str : char.ToLower(str[0]) + str.Substring(1);


        /// <summary>This auxiliary function returns part of the string which begins
        /// with some specified substring and ends with some specified substring.</summary>
        /// <param name="src">Source string.</param>
        /// <param name="startTag">Substring with which the resultant string
        /// must begin. Can be null or empty, in this case the source string is returned
        /// from the very beginning.</param>
        /// <param name="endTag">Substring that the resultant string
        /// must end with. Can be null or empty, in this case the source string is returned
        /// to the very end.</param>
        /// <returns>Portion of the source string.</returns>
        public static string GetSubstring(this string src, string startTag, string endTag)
            => src.GetSubstring(startTag, endTag, false, false, true);


        /// <summary>This auxiliary function returns part of the string which begins
        /// with some specified substring and ends with some specified substring.</summary>
        /// <param name="src">Source string.</param>
        /// <param name="startTag">Substring that the resultant string
        /// must begin with. Can be null or empty, in this case the source string is returned
        /// from the very beginning.</param>
        /// <param name="endTag">Substring that the resultant string
        /// must end with. Can be null or empty, in this case the source string is returned
        /// to the very end.</param>
        /// <param name="removeStartTag">If true, startTag is not included into returned substring.
        /// Default is false.</param>
        /// <param name="removeEndTag">If true, endTag is not included into returned substring.
        /// Default is false.</param>
        /// <param name="raiseExceptionIfTagNotFound">When set to true, raises
        /// ArgumentOutOfRangeException if specified startTag or endTag was not found.
        /// Default is true.</param>
        /// <returns>Part of the source string.</returns>
        public static string GetSubstring(this string src, string startTag, string endTag, bool removeStartTag, bool removeEndTag, bool raiseExceptionIfTagNotFound)
        {
            if (string.IsNullOrEmpty(src))
                throw new ArgumentNullException("src");
            int startPos = 0;
            int endPos = src.Length;

            if (!string.IsNullOrEmpty(startTag))
            {
                startPos = src.IndexOf(startTag);
                if (startPos == -1)
                {
                    if (raiseExceptionIfTagNotFound == true)
                        throw new ArgumentOutOfRangeException("startPos");
                    else
                        startPos = 0;
                }
                else if (removeStartTag)
                    startPos += startTag.Length;
            }

            if (!string.IsNullOrEmpty(endTag))
            {
                endPos = src.IndexOf(endTag, startPos);
                if (endPos == -1)
                {
                    if (raiseExceptionIfTagNotFound == true)
                        throw new ArgumentOutOfRangeException("endPos");
                    else
                        endPos = src.Length;
                }
                else if (!removeEndTag)
                    endPos += endTag.Length;
            }

            return src.Substring(startPos, endPos - startPos);
        }


        /// <summary>This auxiliary function returns the zero-based indexes of all occurrences
        /// of specified string in specified text.</summary>
        /// <param name="text">String to look in.</param>
        /// <param name="str">String to look for.</param>
        /// <param name="ignoreCase">Pass "true" if you require case-insensitive search.
        /// Case-sensitive search is faster.</param>
        /// <returns>Returns the List&lt;int&gt; object containing zero-based indexes of all found 
        /// occurrences or empty List&lt;int&gt; if nothing was found.</returns>
        public static List<int> GetMatchesPositions(this string text, string str, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException("text");
            }

            if (string.IsNullOrEmpty(str))
            {
                throw new ArgumentNullException("str");
            }

            List<int> positions = new List<int>();

            int position = 0;
            StringComparison rule = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            while ((position = text.IndexOf(str, position, rule)) != -1)
            {
                positions.Add(position);
                position++;
            }

            return positions;
        }

        /// <summary>This auxiliary function counts the occurrences of specified string
        /// in specified text. This count is often required, but strangely there is no
        /// such function in .NET Framework's String class.</summary>
        /// <param name="text">String to look in.</param>
        /// <param name="str">String to look for.</param>
        /// <param name="ignoreCase">Pass "true" if you require case-insensitive search.
        /// Case-sensitive search is faster.</param>
        /// <returns>Returns the number of found occurrences.</returns>
        /// <example>
        /// <code>int m = Bot.CountMatches("Bot Bot bot", "Bot", false); // m=2</code>
        /// </example>
        public static int CountMatches(this string text, string str, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException("text");
            }

            if (string.IsNullOrEmpty(str))
            {
                throw new ArgumentNullException("str");
            }

            int matches = 0;
            int position = 0;

            StringComparison rule = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            while ((position = text.IndexOf(str, position, rule)) != -1)
            {
                matches++;
                position++;
            }

            return matches;
        }


        

        public static string HtmlDecode(this string str)
        {
            return HttpUtility.HtmlDecode(str);
        }

        /// <summary>This wrapper function encodes string for use in URI.
        /// The function is necessary because Mono framework doesn't support HttpUtility.UrlEncode()
        /// method and Uri.EscapeDataString() method doesn't support long strings, so a loop is
        /// required. By the way HttpUtility.UrlDecode() is supported by Mono, and a functions
        /// pair Uri.EscapeDataString()/HttpUtility.UrlDecode() is commonly recommended for
        /// encoding/decoding. Although there is another trouble with Uri.EscapeDataString():
        /// prior to .NET 4.5 it doesn't support RFC 3986, only RFC 2396.
        /// </summary>
        /// <param name="str">String to encode.</param>
        /// <returns>Encoded string.</returns>
        public static string UrlEncode(this string str)
        {
            int limit = 32766;    // 32766 is the longest string allowed in Uri.EscapeDataString()

            if (str.Length <= limit)
            {
                return Uri.EscapeDataString(str);
            }

            StringBuilder sb = new StringBuilder(str.Length);
            int portions = str.Length / limit;

            for (int i = 0; i <= portions; i++)
            {
                if (i < portions)
                    sb.Append(Uri.EscapeDataString(str.Substring(limit * i, limit)));
                else
                    sb.Append(Uri.EscapeDataString(str.Substring(limit * i)));
            }

            return sb.ToString();
        }
    }
}
