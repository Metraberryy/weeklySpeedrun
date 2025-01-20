using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class ViewComponentInteractions : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;
    
    public ViewComponentInteractions(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
    }
    
    [ComponentInteraction("view-week", true)]
    public async Task ViewWeek()
    {
        // check if the week is assigned to the current guild
        ulong id = ulong.Parse(Context.Interaction.Data.Values.First());
        WeekEntity? week = await _dbContext.Weeks.FindAsync(id);
        if (week == null 
            || week.GuildId != Context.Guild.Id)
        {
            await RespondAsync("That week doesn't exist!", ephemeral: true);
            return;
        }
        
        EmbedBuilder eb = _dbContext.LeaderboardBuilder(_client, week, null, false);
        await Context.Interaction.Message.ModifyAsync(m => m.Embed = eb.Build());
        await DeferAsync();
    }
}