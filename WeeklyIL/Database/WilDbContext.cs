using Microsoft.EntityFrameworkCore;

namespace WeeklyIL.Database;

public class WilDbContext : DbContext
{
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<WeekEntity> Weeks { get; set; }
    public DbSet<ScoreEntity> Scores { get; set; }
    
    public string DbPath { get; }

    public WilDbContext()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DbPath = Path.Join(path, "WilBot/WilBot.db");
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

