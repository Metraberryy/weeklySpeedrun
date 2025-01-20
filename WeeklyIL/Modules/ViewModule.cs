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

        WeekEntity? nw = null;
        if (isCurrent)
        {
            nw = _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                .Where(w => w.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .OrderBy(w => w.StartTimestamp)
                .FirstOrDefault();
        }
        EmbedBuilder eb = _dbContext.LeaderboardBuilder(_client, week, nw, showVideo);
        SelectMenuBuilder sb = new SelectMenuBuilder()
            .WithPlaceholder("Select a week")
            .WithCustomId("view-week");
        foreach (WeekEntity w in _dbContext.Weeks.Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                     .Where(w => w.StartTimestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                     .OrderByDescending(w => w.Id))
        {
            sb.AddOption(w.Level, w.Id.ToString(), $"ID: {w.Id}");
        }
        ComponentBuilder cb = ComponentBuilder.FromComponents([sb.Build()]);
        
        await RespondAsync(embed: eb.Build(), components: cb.Build(), ephemeral: secret);
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
}