using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Services;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("month", "Commands for managing months")]
public class MonthModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;

    public MonthModule(IDbContextFactory<WilDbContext> contextFactory)
    {
        _dbContext = contextFactory.CreateDbContext();
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
    
    [SlashCommand("new", "Create a new month")]
    public async Task NewMonth(SocketRole? role = null)
    {
        if (await PermissionsFail())
        {
            return;
        }
        
        var entry = await _dbContext.AddAsync(new MonthEntity
        {
            GuildId = Context.Guild.Id,
            RoleId = role?.Id
        });
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync(
            $"Month created! `id: {entry.Entity.Id}`\n" +
            $"Add weeks to it with `/month include <week id> {entry.Entity.Id}`.",
            ephemeral: true);
    }

    [SlashCommand("include", "Adds a week to the month")]
    public async Task IncludeWeek(ulong weekId, ulong monthId)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

        WeekEntity? week = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == weekId);

        if (week == null)
        {
            await RespondAsync("Week doesn't exist!", ephemeral: true);
            return;
        }

        MonthEntity? month = _dbContext.Months
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == monthId);

        if (month == null)
        {
            await RespondAsync("Month doesn't exist!", ephemeral: true);
            return;
        }

        week.MonthId = monthId;
        await _dbContext.SaveChangesAsync();

        await RespondAsync($"Successfully added week {weekId} to month {monthId}!", ephemeral: true);
    }
}