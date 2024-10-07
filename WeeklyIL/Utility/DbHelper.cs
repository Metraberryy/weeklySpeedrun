using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;

namespace WeeklyIL.Utility;

public static class DbHelper
{
    public static async Task CreateGuildIfNotExists(this WilDbContext dbContext, ulong id)
    {
        if (dbContext.Guilds.Any(g => g.Id == id))
        {
            return;
        }

        await dbContext.Guilds.AddAsync(new GuildEntity
        {
            Id = id
        });
        
        await dbContext.SaveChangesAsync();
    }
    
    public static async Task CreateUserIfNotExists(this WilDbContext dbContext, ulong id)
    {
        if (dbContext.Users.Any(g => g.Id == id))
        {
            return;
        }

        await dbContext.Users.AddAsync(new UserEntity
        {
            Id = id
        });
        
        await dbContext.SaveChangesAsync();
    }
    
    public static WeekEntity? CurrentWeek(this WilDbContext dbContext, ulong guild) =>
        dbContext.Weeks
            .Where(w => w.GuildId == guild).AsEnumerable()
            .Where(w => w.StartTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .OrderByDescending(w => w.StartTimestamp).FirstOrDefault();
    
    public static WeekEntity? NextWeek(this WilDbContext dbContext, ulong guild) =>
        dbContext.Weeks
            .Where(w => w.GuildId == guild).AsEnumerable()
            .Where(w => w.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .OrderBy(w => w.StartTimestamp).FirstOrDefault();
    
    public static async Task<bool> UserIsOrganizer(this WilDbContext dbContext, SocketInteractionContext context)
    {
        await dbContext.CreateGuildIfNotExists(context.Guild.Id);
        
        GuildEntity g = dbContext.Guild(context.Guild.Id);
        
        return context.User is SocketGuildUser user
               && user.Roles.Any(r =>
                   r.Id == g.OrganizerRole
                   || r.Id == g.ModeratorRole);
    }

    public static GuildEntity Guild(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Guilds.Find(id)!;
    }
    
    public static MonthEntity Month(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Months.Find(id)!;
    }
    
    public static WeekEntity Week(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Weeks.Find(id)!;
    }
    
    public static UserEntity User(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Users.Find(id)!;
    }
    
    public static EmbedBuilder LeaderboardBuilder(this WilDbContext dbContext, DiscordSocketClient client, WeekEntity week, WeekEntity? nextWeek, bool forceVideo, bool showObsolete = false)
    {
        string board = string.Empty;
        int place = 1;
        var scores = dbContext.Scores
            .Where(s => s.WeekId == week.Id)
            .Where(s => s.Verified);
        if (!showObsolete)
        {
            scores = scores
                .GroupBy(s => s.UserId)
                .Select(g => g.OrderBy(s => s.TimeMs).First());
        }

        client.GetGuild(week.GuildId).DownloadUsersAsync().Wait();
        
        foreach (ScoreEntity score in scores.AsEnumerable().OrderBy(s => s.TimeMs))
        {
            SocketUser? u = client.GetUser(score.UserId);
            string name = u == null ? "unknown" : u.Username;
            
            if (score.Video == null)
            {
                board += $":heavy_multiplication_x: - `??:??.???` - {name}\n";
                continue;
            }
            
            board += place switch
            {
                1 => ":first_place:",
                2 => ":second_place:",
                3 => ":third_place:",
                _ => ":checkered_flag:"
            };
            var ts = new TimeSpan((long)score.TimeMs! * TimeSpan.TicksPerMillisecond);
            board += $@" - `{ts:mm\:ss\.fff}` - ";
            board += forceVideo || week.ShowVideo ? $"[{name}]({score.Video})" : name;
            if (showObsolete) board += $" : {score.Id}";
            board += "\n";
            
            place++;
        }

        var eb = new EmbedBuilder()
            .WithDescription(board)
            .WithFooter($"ID: {week.Id}");

        Uri? uri = week.Level.GetUriFromString();
        if (uri == null)
        {
            eb.WithAuthor(week.Level);
        }
        else
        {
            eb.WithAuthor(week.Level, url: uri.OriginalString);
        }

        if (nextWeek == null) return eb;
        
        var remaining = new TimeSpan((nextWeek.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) * TimeSpan.TicksPerSecond);
        eb.WithTitle($"{remaining.Days}d{remaining:hh}h{remaining:mm}m remaining");

        return eb;
    }
    
    public static EmbedBuilder LeaderboardBuilder(this WilDbContext dbContext, DiscordSocketClient client, MonthEntity month)
    {
        string board = string.Empty;
        int place = 1;

        var weeks = dbContext.Weeks.Where(w => w.MonthId == month.Id); // get weeks in month
        
        foreach (var score in weeks
                     .SelectMany(w => dbContext.Scores.Where(s => s.WeekId == w.Id)) // get scores from every week
                     .Where(s => s.Verified) // keep verified runs
                     .Where(s => s.Video != null) // keep scores with video
                     .GroupBy(s => s.UserId).AsEnumerable() // group scores by user id
                     .Where(g => g.Select(s => s.WeekId).Distinct().Count() == weeks.Count()) // keep runs from users who have a video on each week
                     .Select(g => new
                     {
                         UserId = g.Key,
                         TimeMs = g
                             .GroupBy(s => s.WeekId) // group user's scores by week id
                             .Select(std => std.OrderBy(s => s.TimeMs).First().TimeMs) // get the best for each week
                             .Aggregate(0U, (total, time) => total + (uint)time!) // combine best times
                     })
                     .OrderBy(result => result.TimeMs)) // order by time
        {
            string name = client.GetUser(score.UserId).Username;
            
            board += place switch
            {
                1 => ":first_place:",
                2 => ":second_place:",
                3 => ":third_place:",
                _ => ":checkered_flag:"
            };
            var ts = new TimeSpan(score.TimeMs * TimeSpan.TicksPerMillisecond);
            board += $@" - `{ts:h\:mm\:ss\.fff}` - {name}";
            place++;
        }

        string title = month.RoleId == null
            ? $"Month {month.Id}"
            : $"{client.GetGuild(month.GuildId).GetRole((ulong)month.RoleId).Name} month";

        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(board)
            .WithFooter($"ID: {month.Id}; Weeks: {string.Join(", ", weeks.Select(w => w.Id))}");
    }
}