using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;

namespace WeeklyIL.Utility;

public static class DbHelper
{
    public static async Task CreateIfNotExists(this WilDbContext dbContext, SocketGuild guild)
    {
        if (dbContext.Guilds.Any(g => g.Id == guild.Id))
        {
            return;
        }

        await dbContext.Guilds.AddAsync(new GuildEntity
        {
            Id = guild.Id
        });
        
        await dbContext.SaveChangesAsync();
    }
    
    public static WeekEntity? CurrentWeek(this WilDbContext dbContext, SocketGuild guild) =>
        dbContext.Weeks
            .Where(w => w.GuildId == guild.Id).AsEnumerable()
            .Where(w => w.StartTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .OrderByDescending(w => w.StartTimestamp).FirstOrDefault();
    
    public static async Task<bool> UserIsOrganizer(this WilDbContext dbContext, SocketInteractionContext iContext)
    {
        await dbContext.CreateIfNotExists(iContext.Guild);
        
        GuildEntity g = dbContext.Guilds.First(g => g.Id == iContext.Guild.Id);
        
        return iContext.User is SocketGuildUser user
               && user.Roles.Any(r =>
                   r.Id == g.OrganizerRole
                   || r.Id == g.ModeratorRole);
    }
}