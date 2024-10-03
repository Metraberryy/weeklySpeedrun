using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("view", "Commands for viewing weeks")]
public class ViewModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public ViewModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
    }
    
    private async Task<bool> PermissionsFail()
    {
        if (await _dbContext.UserIsOrganizer(Context))
        {
            return false;
        }

        await RespondAsync("You can't do that here!", ephemeral: true);
        return true;
    }
    
    [SlashCommand("week", "Shows the leaderboard for a week")]
    public async Task ViewWeek(ulong? id = null)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        
        // check if the week is assigned to the current guild
        WeekEntity? week = id == null 
            ? _dbContext.CurrentWeek(Context.Guild.Id) 
            : _dbContext.Weeks.FirstOrDefault(w => w.Id == id);
        
        bool secret = week?.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool organizer = await _dbContext.UserIsOrganizer(Context);
        
        if (week == null 
            || week.GuildId != Context.Guild.Id
            || (secret && !organizer))
        {
            await RespondAsync("That week doesn't exist!", ephemeral: true);
            return;
        }

        bool isCurrent = week.Id == _dbContext.CurrentWeek(Context.Guild.Id)?.Id;
        bool showVideo = !isCurrent || organizer || week.ShowVideo;
        secret |= isCurrent && showVideo && !week.ShowVideo;

        EmbedBuilder eb = _dbContext.LeaderboardBuilder(_client, week, showVideo);
        
        await RespondAsync(embed: eb.Build(), ephemeral: secret);
    }
    
    [SlashCommand("month", "Shows the leaderboard for a month")]
    public async Task ViewMonth(ulong? id = null)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        
        // check if the week is assigned to the current guild
        id ??= _dbContext.CurrentWeek(Context.Guild.Id)?.MonthId ?? 0;
        MonthEntity? month = _dbContext.Months.FirstOrDefault(m => m.Id == id);
        
        bool secret = _dbContext.Weeks
            .Where(w => w.MonthId == id).AsEnumerable()
            .Any(w => w.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        bool organizer = await _dbContext.UserIsOrganizer(Context);
        
        if (month == null 
            || month.GuildId != Context.Guild.Id
            || (secret && !organizer))
        {
            await RespondAsync("That month doesn't exist!", ephemeral: true);
            return;
        }

        EmbedBuilder eb = _dbContext.LeaderboardBuilder(_client, month);

        await RespondAsync(embed: eb.Build(), ephemeral: secret);
    }
    
    [SlashCommand("past", "Shows previous weeks")]
    public async Task PastWeeks()
    {
        WeekEntity? we = _dbContext.CurrentWeek(Context.Guild.Id);
        if (we == null)
        {
            await RespondAsync("nope");
            return;
        }
        
        var eb = new EmbedBuilder().WithTitle("Previous weeks");
        foreach (WeekEntity week in _dbContext.Weeks
                     .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                     .Where(w => w.StartTimestamp < we.StartTimestamp)
                     .OrderByDescending(w => w.StartTimestamp))
        {
            eb.AddField($"<t:{week.StartTimestamp}:D> ID: {week.Id}", week.Level);
        }
        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }
    
    [SlashCommand("queue", "Shows the queue of upcoming weeks")]
    public async Task WeeksQueue()
    {
        if (await PermissionsFail())
        {
            return;
        }

        var eb = new EmbedBuilder().WithTitle("Upcoming weeks");
        foreach (WeekEntity week in _dbContext.Weeks
                     .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                     .Where(w => w.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                     .OrderBy(w => w.StartTimestamp))
        {
            eb.AddField($"<t:{week.StartTimestamp}:D> ID: {week.Id}", week.Level);
        }
        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }
}