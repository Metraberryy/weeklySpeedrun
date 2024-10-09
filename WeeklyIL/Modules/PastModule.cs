using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("past", "Commands for viewing previous things")]
public class PastModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public PastModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
    }
    
    [SlashCommand("weeks", "Shows previous weeks")]
    public async Task PastWeeks()
    {
        WeekEntity? we = _dbContext.CurrentWeek(Context.Guild.Id);
        if (we == null)
        {
            await RespondAsync("nope", ephemeral: true);
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
    
    [SlashCommand("months", "Shows previous months")]
    public async Task PastMonths()
    {
        WeekEntity? we = _dbContext.CurrentWeek(Context.Guild.Id);
        if (we == null)
        {
            await RespondAsync("nope", ephemeral: true);
            return;
        }
        
        var eb = new EmbedBuilder().WithTitle("Previous months");
        foreach (MonthEntity month in _dbContext.Weeks
                     .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                     .GroupBy(w => w.MonthId)
                     .Select(g => g.OrderByDescending(w => w.StartTimestamp).First())
                     .Where(w => w.MonthId != null)
                     .Where(w => w.StartTimestamp < we.StartTimestamp)
                     .Select(w => _dbContext.Month((ulong)w.MonthId)))
        {
            string? name = _client.GetGuild(Context.Guild.Id).GetRole((ulong)month.RoleId).Mention;
            name += $"ID: {month.Id}";
            eb.AddField(name, $"Weeks: {string.Join(", ", _dbContext.Weeks.Where(w => w.MonthId == month.Id).Select(w => w.Id))}");
        }
        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }
}