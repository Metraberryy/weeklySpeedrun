using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Services;

public class WeekEndTimers
{
    private readonly Dictionary<ulong, HashSet<Timer>> _allTimers = new();

    private readonly uint[] _intervals =
    [
        86400U, // 1 day
        3600U, // 1 hour
        600U, // 10 minutes
        0U // real one
    ];

    private readonly IDbContextFactory<WilDbContext> _contextFactory;
    private readonly DiscordSocketClient _client;
    
    public WeekEndTimers(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _contextFactory = contextFactory;
        _client = client;
    }
    
    public async Task UpdateGuildTimer(ulong id)
    {
        // i have to create a new dbcontext here to avoid cache issues
        WilDbContext dbContext = await _contextFactory.CreateDbContextAsync();

        WeekEntity? nextWeek = dbContext.NextWeek(id);
        if (nextWeek == null) return;

        bool notnull = _allTimers.TryGetValue(id, out var timers);
        if (notnull)
        {
            foreach (Timer timer in timers!)
            {
                await timer.DisposeAsync();
            }
            timers.Clear();
        }
        else
        {
            timers = [];
            _allTimers.Add(id, timers);
        }

        for (int i = 0; i < _intervals.Length; i++)
        {
            uint interval = _intervals[i];
            long seconds = nextWeek.StartTimestamp - interval - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (seconds < 0) continue;
            
            var dueTime = new TimeSpan(seconds * TimeSpan.TicksPerSecond);
            timers.Add(i == _intervals.Length - 1
                ? new Timer(o => OnWeekEnd(o), dbContext.CurrentWeek(id), dueTime, Timeout.InfiniteTimeSpan)
                : new Timer(o => OnCountdown(o), nextWeek, dueTime, Timeout.InfiniteTimeSpan));
        }
        _allTimers[id] = timers;
    }
    
    private async Task OnCountdown(object? o)
    {
        var week = (WeekEntity)o!;
        
        long seconds = week.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = new TimeSpan(seconds * TimeSpan.TicksPerSecond);
        
        WilDbContext dbContext = await _contextFactory.CreateDbContextAsync();
        SocketGuild guild = _client.GetGuild(week.GuildId);
        SocketTextChannel channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);

        await channel.SendMessageAsync($@"Week ends in `{Math.Floor(remaining.TotalHours)}h {remaining:mm}m`!");
    }
    
    private async Task OnWeekEnd(object? o)
    {
        await Task.Delay(1000); // trying to avoid race conditions lmao

        if (o == null) return; // this is the first week in the guild, im too lazy rn to make it announce anything
        var week = (WeekEntity)o;
        
        WilDbContext dbContext = await _contextFactory.CreateDbContextAsync();
        SocketGuild guild = _client.GetGuild(week.GuildId);
        SocketTextChannel channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);

        if (!await TryEndWeek(week))
        {
            await channel.SendMessageAsync("Week ended! Results will be posted when all currently pending runs are verified.");
        }
    }

    public async Task<bool> TryEndWeek(WeekEntity week)
    {
        WilDbContext dbContext = await _contextFactory.CreateDbContextAsync();

        await UpdateGuildTimer(week.GuildId);

        SocketGuild guild = _client.GetGuild(week.GuildId);
        SocketTextChannel channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);

        if (dbContext.Scores.Where(s => s.WeekId == week.Id).Any(s => !s.Verified))
        {
            return false;
        }
        
        week.Ended = true;
        dbContext.Update(week);
        await dbContext.SaveChangesAsync();
        
        EmbedBuilder eb = dbContext.LeaderboardBuilder(_client, week, null, true);
        await channel.SendMessageAsync("Week ended! This is the leaderboard as of now:", embed: eb.Build());

        ScoreEntity? first = dbContext.Scores
            .Where(s => s.WeekId == week.Id)
            .Where(s => s.Verified)
            .OrderBy(s => s.TimeMs)
            .FirstOrDefault();

        if (first == null) return true; // sad

        await dbContext.CreateUserIfNotExists(first.UserId);

        UserEntity ue = dbContext.User(first.UserId);
        ue.WeeklyWins++;

        SocketGuildUser? user = guild.GetUser(first.UserId);
        await user.AddRolesAsync(dbContext.Guilds
            .Include(g => g.WeeklyRoles)
            .First(g => g.Id == week.GuildId).WeeklyRoles
            .Where(r => r.Requirement <= ue.WeeklyWins)
            .Select(r => r.RoleId));

        await dbContext.SaveChangesAsync();

        if (week.MonthId == null) return true;

        var weeks = dbContext.Weeks.Where(w => w.MonthId == week.MonthId); // get weeks in month

        var monthFirst = weeks
            .SelectMany(w => dbContext.Scores.Where(s => s.WeekId == w.Id)) // get scores from every week
            .Where(s => s.Verified) // keep verified runs
            .Where(s => s.Video != null) // keep scores with video
            .GroupBy(s => s.UserId).AsEnumerable() // group scores by user id
            .Where(g => g.Select(s => s.WeekId).Distinct().Count() ==
                        weeks.Count()) // keep runs from users who have a video on each week
            .Select(g => new
            {
                UserId = g.Key,
                TimeMs = g
                    .GroupBy(s => s.WeekId) // group user's scores by week id
                    .Select(std => std.OrderBy(s => s.TimeMs).First().TimeMs) // get the best for each week
                    .Aggregate((uint)0, (total, time) => total + (uint)time!) // combine best times
            })
            .OrderBy(result => result.TimeMs) // order by time
            .FirstOrDefault();

        if (monthFirst == null) return true; // also sad

        user = guild.GetUser(monthFirst.UserId);
        dbContext.User(monthFirst.UserId).MonthlyWins++;
        await dbContext.SaveChangesAsync();

        ulong? rid = dbContext.Month((ulong)week.MonthId).RoleId;
        if (rid == null) return true;
        await user.AddRoleAsync((ulong)rid);
        
        return true;
    }

    public async Task DisposeAsync()
    {
        foreach (Timer t in _allTimers.SelectMany(kvp => kvp.Value))
        {
            await t.DisposeAsync();
        }
    }
}