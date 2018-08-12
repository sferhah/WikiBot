using WikiBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KabWikiBot
{
    public class KabLatinizerBot : Bot
    {
        public async Task<int> DoJobAsync(DateTime minDate)
        {
            var botPage = await base.GetPageByTitleAsync($"User:{base.UserName}/Stalked Users");

            if (botPage == null)
            {
                return 0;
            }

            var stalkedUsers = botPage.Content.GetStalkedUsers();
            var suspectedUsers = botPage.Content.GetSuspectedUsers();

            var recent_changes = (await base.GetRecentChangesAsync(500)).Where(x => x.Timestamp > minDate).ToArray();
            var new_suspects = await this.FindNewSuspectedUsers(recent_changes, stalkedUsers.Concat(suspectedUsers));

            foreach (var new_suspect in new_suspects)
            {
                botPage.Content.AddNewSuspect(new_suspect);
            }

            if (new_suspects.Any())
            {
                await base.SavePageAsync(botPage, "add user(s)", false);
            }

            return await this.LatinizeAsync(recent_changes, stalkedUsers);
        }

        public async Task<int> LatinizeAsync(RecentChange[] recent_changes, IEnumerable<string> stalkedUsers)
        {
            var distinctPages = recent_changes
                .Where(x => stalkedUsers.Contains(x.User))
                .Select(x => x.Title)
                .Distinct()
                .ToList();

            List<Page> invalidPages = new List<Page>();

            foreach (var title in distinctPages)
            {
                var page = await base.GetPageByTitleAsync(title);

                if (page == null)
                {
                    continue;
                }

                if (page.Content.IsInvalid())
                {
                    invalidPages.Add(page);
                }
            }

            foreach (var page in invalidPages)
            {
                page.Content.ReplaceGreekLetters();
                await base.SavePageAsync(page, "Replace greek letters with latin letters.", true);
            }

            return invalidPages.Count;
        }

        public async Task<List<string>> FindNewSuspectedUsers(RecentChange[] recent_changes, IEnumerable<string> suspectedOrConfirmed)
        {
            List<string> suspectedUsers = new List<string>();

            foreach (var change in recent_changes)
            {
                bool isSuspected = false;

                if (change.Type == "new")
                {
                    var page = await base.GetPageByRevisionIdAsync(change.Revid);

                    if (page.Content.Text.IndexOfAny(PageContentExtensions.dictionary.Keys.ToArray()) != -1)
                    {
                        isSuspected = true;
                    }
                }
                else if (change.Type == "edit")
                {
                    var cmpr = await base.GetDiffAsync(change.OldRevid, change.Revid);

                    if (cmpr == null)
                    {
                        continue;
                    }

                    if (cmpr.Insertions.Where(x => x.IndexOfAny(PageContentExtensions.dictionary.Keys.ToArray()) != -1).Any())
                    {
                        isSuspected = true;
                    }
                }

                if (isSuspected
                    && !suspectedOrConfirmed.Contains(change.User))
                {
                    suspectedUsers.Add(change.User);
                }
            }

            return suspectedUsers;
        }
    }
}
