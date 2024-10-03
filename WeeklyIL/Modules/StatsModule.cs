using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class StatsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public StatsModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
    }
    
    [SlashCommand("stats", "Get stats for a user")]
    [RequireUserPermission(GuildPermission.ManageChannels)]
    public async Task GetStats(SocketGuildUser? user = null)
    {
        user ??= _client.GetGuild(Context.Guild.Id).GetUser(Context.User.Id);
        
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        await _dbContext.CreateUserIfNotExists(user.Id);

        UserEntity ue = _dbContext.User(user.Id);

        var totalTime = _dbContext.Scores
            .Where(s => s.UserId == user.Id)
            .Where(s => s.Video != null)
            .Where(s => s.Verified).AsEnumerable()
            .Where(s => _dbContext.Week(s.WeekId).GuildId == Context.Guild.Id)
            .Aggregate(0U, (total, s) => total + (uint)s.TimeMs);
        var ts = new TimeSpan(totalTime * TimeSpan.TicksPerMillisecond);
        
        string desc = $"Total run time: `{ts:d\\:hh\\:mm\\:ss\\.fff}`\n" +
                      $"Weekly wins: `{ue.WeeklyWins}`\n" +
                      $"Monthly wins: `{ue.MonthlyWins}`";

        var eb = new EmbedBuilder()
            .WithTitle($"{user.Username}'s stats")
            .WithDescription(desc)
            .WithColor(user.Roles.OrderByDescending(r => r.Position).First().Color);

        await RespondAsync(embed: eb.Build());
    }
}