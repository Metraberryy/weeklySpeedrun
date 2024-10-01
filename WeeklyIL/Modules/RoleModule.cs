using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("role", "Commands for setting up permissions roles")]
public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;

    public RoleModule(WilDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    [SlashCommand("moderator", "Sets the moderator permissions role")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetModRole(SocketRole role)
    {
        await _dbContext.CreateIfNotExists(Context.Guild);
        
        _dbContext.Guilds.First(g => g.Id == Context.Guild.Id).ModeratorRole = role.Id;
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync($"Successfully set moderator role to {role.Mention}!", ephemeral: true);
    }
    
    [SlashCommand("organizer", "Sets the organizer permissions role")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetOrgRole(SocketRole role)
    {
        await _dbContext.CreateIfNotExists(Context.Guild);
        
        _dbContext.Guilds.First(g => g.Id == Context.Guild.Id).OrganizerRole = role.Id;
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync($"Successfully set organizer role to {role.Mention}!", ephemeral: true);
    }
}