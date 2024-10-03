using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("scores", "Commands for managing scores")]
public class ScoreModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public ScoreModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
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

    [SlashCommand("clear", "Removes all scores from week by id")]
    public async Task ClearWeek(ulong? week = null)
    {
        if (await PermissionsFail())
        {
            return;
        }
        
        ulong weekId = week ?? _dbContext.CurrentWeek(Context.Guild.Id)?.Id ?? 0;
        WeekEntity? we = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == weekId);
        
        if (we == null
            || (we.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds() 
                && !await _dbContext.UserIsOrganizer(Context)))
        {
            await RespondAsync("No week to clear!", ephemeral: true);
            return;
        }
        
        _dbContext.Scores.RemoveRange(_dbContext.Scores.Where(s => s.WeekId == weekId));
        await _dbContext.SaveChangesAsync();

        await RespondAsync($"Successfully cleared scores for week {weekId}!", ephemeral: true);
    }
}