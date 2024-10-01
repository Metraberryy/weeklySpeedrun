using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("channel", "Commands for setting up channels")]
public class ChannelModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;

    public ChannelModule(WilDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    [SlashCommand("submissions", "Sets the channel that submissions go to")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SetSubmissionChannel(SocketGuildChannel channel)
    {
        await _dbContext.CreateIfNotExists(Context.Guild);

        if (channel.GetChannelType() != ChannelType.Text)
        {
            await RespondAsync($"<#{channel.Id}> isn't a text channel!", ephemeral: true);
            return;
        }
        
        _dbContext.Guilds.First(g => g.Id == Context.Guild.Id).SubmissionsChannel = channel.Id;
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync($"Successfully set submissions channel to <#{channel.Id}>!", ephemeral: true);
    }
}