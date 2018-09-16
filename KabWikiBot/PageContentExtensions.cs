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

        public static bool ReplaceGreekLetters(this PageContent pageContent)
        {
            string originalText = pageContent.Text;

            foreach (var kv in dictionary)
            {
                originalText = originalText.Replace(kv.Key, kv.Value);
            }

            if(pageContent.Text == originalText)
            {
                return false;
            }

            pageContent.Text = originalText;

            return true;
        }

        public static string[] GetStalkedUsers(this PageContent pageContent)        
              => pageContent.GetUsers("Confirmed");

        public static string[] GetPendingUsers(this PageContent pageContent)
              => pageContent.GetUsers("Pending");

        public static string[] GetSuspectedUsers(this PageContent pageContent)
            => pageContent.GetUsers("Suspected");

        private static string[] GetUsers(this PageContent pageContent, string sectionName) 
            => pageContent.Sections.Where(x => x.Title == sectionName).FirstOrDefault()?
                .Text.Replace("\n", string.Empty)
                .Split("*", StringSplitOptions.RemoveEmptyEntries)
                ?? new string[] { };

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
