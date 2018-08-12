using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace KabWikiBot
{
    [Table("GeoLog")]
    public class GeoLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
        public string Error { get; set; }
        public int NumberOfModifiedPages { get; set; }

        [NotMapped]
        public GeoLogState State => (EndDate == null) ? GeoLogState.Pending : ((Error != null) ? GeoLogState.Failed : GeoLogState.Done);
    }

    public enum GeoLogState
    {
        Pending,
        Done,
        Failed,
    }

    public class AppDbContext : DbContext
    {
        public DbSet<GeoLog> Logs { get; set; }        

        public static async Task InitAsync()
        {
            using (AppDbContext database = new AppDbContext())
            {
                await database.Database.EnsureCreatedAsync();
                await database.Database.MigrateAsync();
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite($"Filename=logs.db3");
    }
}
