using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("role", "Commands for setting up permissions roles")]
public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;

    public RoleModule(IDbContextFactory<WilDbContext> contextFactory)
    {
        _dbContext = contextFactory.CreateDbContext();
    }
    
    [SlashCommand("moderator", "Sets the moderator permissions role")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task SetModRole(SocketRole role)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        
        _dbContext.Guild(Context.Guild.Id).ModeratorRole = role.Id;
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync($"Successfully set moderator role to {role.Mention}!", ephemeral: true);
    }
    
    [SlashCommand("organizer", "Sets the organizer permissions role")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task SetOrgRole(SocketRole role)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        
        _dbContext.Guild(Context.Guild.Id).OrganizerRole = role.Id;
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync($"Successfully set organizer role to {role.Mention}!", ephemeral: true);
    }
    
    [SlashCommand("weekly", "Sets a role for a certain number of weekly WRs")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task SetWeeklyRole(int requirement, SocketRole role)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

        if (requirement < 1)
        {
            await RespondAsync($"Requirement cannot be less than 1.", ephemeral: true);
            return;
        }
        
        GuildEntity guild = _dbContext.Guilds
            .Include(g => g.WeeklyRoles)
            .First(g => g.Id == Context.Guild.Id);
        var roles = guild.WeeklyRoles.Where(r => r.Requirement == requirement || r.RoleId == role.Id);
        foreach (WeeklyRole wr in roles)
        {
            guild.WeeklyRoles.Remove(wr);
        }
        guild.WeeklyRoles.Add(new WeeklyRole { Requirement = (uint)requirement, RoleId = role.Id });

        await _dbContext.SaveChangesAsync();

        string word = requirement > 1 ? "weeklies" : "weekly";
        await RespondAsync($"Successfully set \"{requirement} {word}\" role to {role.Mention}!", ephemeral: true);
    }
    
    [SlashCommand("game", "Sets a role for a game")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task GameRole(string? game = null, SocketRole? role = null)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

        if (game == null && role == null)
        {
            string desc = _dbContext.Guilds
                .Include(g => g.GameRoles)
                .First(g => g.Id == Context.Guild.Id).GameRoles
                .Aggregate("", (current, gr) => current + $"{gr.Game} : {Context.Guild.GetRole(gr.RoleId).Mention}");
            var eb = new EmbedBuilder()
                .WithTitle("Game roles")
                .WithDescription(desc);
            await RespondAsync(embed: eb.Build(), ephemeral: true);
            return;
        }

        if (game == null)
        {
            await RespondAsync($"Game is missing!", ephemeral: true);
            return;
        }
        
        if (role == null)
        {
            await RespondAsync($"Role is missing!", ephemeral: true);
            return;
        }
        
        GuildEntity guild = _dbContext.Guilds
            .Include(g => g.GameRoles)
            .First(g => g.Id == Context.Guild.Id);
        var roles = guild.GameRoles.Where(r => r.Game == game || r.RoleId == role.Id);
        foreach (GameRole gr in roles)
        {
            guild.GameRoles.Remove(gr);
        }
        guild.GameRoles.Add(new GameRole { Game = game, RoleId = role.Id });

        await _dbContext.SaveChangesAsync();

        await RespondAsync($"Successfully set \"{game}\" role to {role.Mention}!", ephemeral: true);
    }
    
    [SlashCommand("monthly", "Sets a role to be awarded to the WR holder of a month by id")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task SetMonthlyRole(ulong id, SocketRole role)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

        MonthEntity? month = _dbContext.Months
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == id);
        
        if (month == null)
        {
            await RespondAsync("Month doesn't exist!");
            return;
        }

        month.RoleId = role.Id;
        await _dbContext.SaveChangesAsync();

        await RespondAsync($"Successfully set month {id}'s role to {role.Mention}!", ephemeral: true);
    }
}