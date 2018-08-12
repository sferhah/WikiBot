using System;
using System.Threading.Tasks;

namespace WikiBot
{
    public static class BotFactory
    {
        /// <summary>This constructor is used for LDAP authentication. Additional information can
        /// be found <see href="http://www.mediawiki.org/wiki/Extension:LDAP_Authentication">here
        /// </see>.</summary>
        /// <param name="address">Wiki site's URI. It must point to the main page of the wiki, e.g.
        /// "https://en.wikipedia.org" or "http://127.0.0.1:80/w/index.php?title=Main_page".</param>
        /// <param name="userName">User name to log in.</param>
        /// <param name="userPass">Password.</param>
        /// <param name="userDomain">Domain name for LDAP authentication.</param>
        /// <returns>Returns Site object.</returns>
        public static async Task<T> CreateInstanceAsync<T>(string address, string userName, string userPass, string userDomain = null) where T : BotBase
        {
            BotBase bot = Activator.CreateInstance(typeof(T), true) as BotBase;

            bot.Address = address;
            bot.UserName = userName;
            bot.UserPass = userPass;
            bot.UserDomain = userDomain;

            await bot.InitializeAsync();

            return (T)bot;
        }
    }

}
