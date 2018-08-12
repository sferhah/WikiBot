using WikiBot;
using System.Collections.Generic;
using System.Linq;
using System;

namespace KabWikiBot
{
    public static class PageContentExtensions
    {
        public static readonly Dictionary<char, char> dictionary = new Dictionary<char, char>()
        {
            { 'Γ', 'Ɣ'} ,
            { 'γ', 'ɣ'} ,
            { 'Σ', 'Ɛ'} ,
            { 'ε', 'ɛ'} ,
            { 'Ğ', 'Ǧ'} ,
            { 'ğ', 'ǧ'} ,
         };

        public static bool IsInvalid(this PageContent pageContent)
        {
            foreach (var kv in dictionary)
            {
                if (pageContent.Text.Contains(kv.Key))
                {
                    return true;
                }
            }

            return false;
        }

        public static void ReplaceGreekLetters(this PageContent pageContent)
        {
            foreach (var kv in dictionary)
            {
                pageContent.Text = pageContent.Text.Replace(kv.Key, kv.Value);
            }
        }

        public static string[] GetStalkedUsers(this PageContent pageContent)
        {
            return pageContent.Sections.Where(x => x.Title == "Confirmed").FirstOrDefault()?
                .Text.Replace("\n", string.Empty)
                .Split("*", StringSplitOptions.RemoveEmptyEntries)
                ?? new string[] { };
        }


        public static string[] GetSuspectedUsers(this PageContent pageContent)
        {
            return pageContent.Sections.Where(x => x.Title == "Suspected").FirstOrDefault()?
                .Text.Replace("\n", string.Empty)
                .Split("*", StringSplitOptions.RemoveEmptyEntries)
                ?? new string[] { };
        }

        public static void AddNewSuspect(this PageContent pageContent, string user)
        {
            var startIndex = pageContent.Text.IndexOf("==Suspected==");

            if(startIndex < 1)
            {
                return;
            }

            pageContent.Text = pageContent.Text.Insert(startIndex + "==Suspected==".Length, "\n*" + user);

        }

    }
}
