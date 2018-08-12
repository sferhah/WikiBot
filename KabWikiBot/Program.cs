using WikiBot;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace KabWikiBot
{
    public class Credentials
    {
        public string Address { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class BotSettings
    {
        public int SaveDelay { get; set; }
        public int MaxLag { get; set; }
        public int RetryCountPerRequest { get; set; }
    }


    class Program
    {
        public static async Task Main(string[] args)
        {
            await AppDbContext.InitAsync();

            using (AppDbContext context = new AppDbContext())
            {
                var lastLog = await context.Logs.OrderByDescending(x => x.Id).FirstOrDefaultAsync();

                //Ensure there's a single instance being executed.
                if (lastLog != null
                    && lastLog.State == GeoLogState.Pending
                    && ((DateTime.UtcNow - lastLog.StartDate).TotalMinutes) < 10) // Pending state might be fake, ignore it after 10 minutes
                {
                    return;
                }
                
                var last_execution_date = lastLog?.EndDate ?? lastLog?.StartDate ?? DateTime.MinValue;                

                var log = new GeoLog { StartDate = DateTime.UtcNow };
                context.Logs.Add(log);
                await context.SaveChangesAsync();

                try
                {
                    IConfigurationRoot configuration = new ConfigurationBuilder()
                      .SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .Build();

                    var credentials = configuration.GetSection("Credentials").Get<Credentials>();
                    var botSettings = configuration.GetSection("BotSettings").Get<BotSettings>();

                  //  credentials = new Credentials { Address = "http://127.0.0.1:81/mediawiki", Username = "admin", Password = "adminadmin" };

                    KabLatinizerBot bot = await BotFactory.CreateInstanceAsync<KabLatinizerBot>(credentials.Address, credentials.Username, credentials.Password);

                    if (botSettings != null)
                    {
                        bot.SaveDelay = botSettings.SaveDelay;
                        bot.MaxLag = botSettings.MaxLag;
                        bot.RetryCountPerRequest = botSettings.RetryCountPerRequest;
                    }

                    log.NumberOfModifiedPages = await bot.DoJobAsync(last_execution_date);

                }
                catch (Exception e)
                {
                    log.Error = e.Message;
                }

                log.EndDate = DateTime.UtcNow;
                
                await context.SaveChangesAsync();
            }

        }    
    }
}
