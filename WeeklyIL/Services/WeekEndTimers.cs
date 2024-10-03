using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Modules;
using WeeklyIL.Utility;

namespace WeeklyIL.Services;

public class WeekEndTimers
{
    public readonly Dictionary<ulong, Timer> Timers = new();

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

        var ts = new TimeSpan(
            (nextWeek.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) * TimeSpan.TicksPerSecond
        );
        var newt = new Timer(o => OnWeekEnd(o), dbContext.CurrentWeek(id), ts, Timeout.InfiniteTimeSpan);
        
        if (Timers.TryGetValue(id, out Timer? timer))
        {
            await timer.DisposeAsync();
            Timers[id] = newt;
            return;
        }
        
        Timers.Add(id, newt);
    }
    
    public async Task OnWeekEnd(object? o)
    {
        await Task.Delay(1000); // trying to avoid race conditions lmao

        if (o == null) return; // this is the first week in the guild, im too lazy rn to make it announce anything
        var week = (WeekEntity)o;

        WilDbContext dbContext = await _contextFactory.CreateDbContextAsync();
        week.Ended = true;
        dbContext.Update(week);
        await dbContext.SaveChangesAsync();

        await UpdateGuildTimer(week.GuildId);

        SocketGuild guild = _client.GetGuild(week.GuildId);
        SocketTextChannel channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);
        EmbedBuilder eb = dbContext.LeaderboardBuilder(_client, week, true);
        await channel.SendMessageAsync("Week ended! This is the leaderboard as of now:", embed: eb.Build());

        ScoreEntity? first = dbContext.Scores
            .Where(s => s.WeekId == week.Id)
            .Where(s => s.Verified)
            .OrderBy(s => s.TimeMs)
            .FirstOrDefault();

        if (first == null) return; // sad

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

        if (week.MonthId == null) return;

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
                    .Aggregate((uint)0, (total, time) => total + (uint)time) // combine best times
            })
            .OrderBy(result => result.TimeMs) // order by time
            .FirstOrDefault();

        if (monthFirst == null) return; // also sad

        user = guild.GetUser(monthFirst.UserId);
        dbContext.User(monthFirst.UserId).MonthlyWins++;
        await dbContext.SaveChangesAsync();

        ulong? rid = dbContext.Month((ulong)week.MonthId).RoleId;
        if (rid == null) return;
        await user.AddRoleAsync((ulong)rid);
    }
}