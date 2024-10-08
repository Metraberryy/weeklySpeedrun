using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Services;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("debug", "Commands for fixing bugs")]
public class DebugModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;
    private readonly WeekEndTimers _weekEnder;

    public DebugModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client, WeekEndTimers weekEnder)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
        _weekEnder = weekEnder;
    }
    
    [SlashCommand("clearstats", "oops")]
    [RequireOwner]
    public async Task ClearStats(SocketGuildUser? user = null)
    {
        user ??= _client.GetGuild(Context.Guild.Id).GetUser(Context.User.Id);
        
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        await _dbContext.CreateUserIfNotExists(user.Id);

        UserEntity ue = _dbContext.User(user.Id);
        ue.WeeklyWins = 0;
        ue.MonthlyWins = 0;
        await _dbContext.SaveChangesAsync();

        await RespondAsync("Success!", ephemeral: true);
    }
    
    [SlashCommand("endweek", "oops")]
    [RequireOwner]
    public async Task EndWeek(ulong id)
    {
        WeekEntity? week = await _dbContext.Weeks.FindAsync(id);
        if (week == null)
        {
            await RespondAsync("That week doesn't exist!", ephemeral: true);
            return;
        }

        if (await _weekEnder.TryEndWeek(week!))
        {
            await RespondAsync("Success!", ephemeral: true);
        }
        else
        {
            await RespondAsync("Failed to end week (pending submissions)", ephemeral: true);
        }
    }
    
    [SlashCommand("unendweek", "oops")]
    [RequireOwner]
    public async Task UnEndWeek(ulong id)
    {
        WeekEntity? week = await _dbContext.Weeks.FindAsync(id);
        if (week == null)
        {
            await RespondAsync("That week doesn't exist!", ephemeral: true);
            return;
        }

        week.Ended = false;
        await _dbContext.SaveChangesAsync();
        await RespondAsync("Success!", ephemeral: true);
    }
}