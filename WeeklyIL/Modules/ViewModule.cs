using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("view", "Commands for viewing weeks")]
public class ViewModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public ViewModule(WilDbContext dbContext, DiscordSocketClient client)
    {
        _dbContext = dbContext;
        _client = client;
    }

    [SlashCommand("current", "Shows the leaderboard of the current week")]
    public async Task CurrentWeek()
    {
        ulong? id = _dbContext.CurrentWeek(Context.Guild)?.Id;
        await ViewWeek(id ?? 0);
    }
    
    [SlashCommand("week", "Shows the leaderboard of the week indicated by id")]
    public async Task ViewWeek(ulong id)
    {
        await _dbContext.CreateIfNotExists(Context.Guild);
        
        // check if the week is assigned to the current guild
        WeekEntity? week = _dbContext.Weeks.FirstOrDefault(w => w.Id == id);
        bool secret = week?.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        if (week == null 
            || week.GuildId != Context.Guild.Id
            || (secret && !await _dbContext.UserIsOrganizer(Context)))
        {
            await RespondAsync("That week doesn't exist!", ephemeral: true);
            return;
        }

        string board = string.Empty;
        int place = 1;
        foreach (ScoreEntity score in _dbContext.Scores
                     .Where(s => s.WeekId == id)
                     .Where(s => s.Verified)
                     .GroupBy(s => s.UserId)
                     .Select(g => g.OrderBy(s => s.TimeMs).First()))
        {
            if (score.Video == null)
            {
                board += $":heavy_multiplication_x: - `??:??.???` - {(await _client.GetUserAsync(score.UserId)).Username}\n";
                continue;
            }
            
            board += place switch
            {
                1 => ":first_place:",
                2 => ":second_place:",
                3 => ":third_place:",
                _ => ":checkered_flag:"
            };
            var ts = new TimeSpan((long)score.TimeMs * TimeSpan.TicksPerMillisecond);
            board += $" - `{ts:mm\\:ss\\.fff}` - [{(await _client.GetUserAsync(score.UserId)).Username}]({score.Video})\n";
            place++;
        }
        
        var eb = new EmbedBuilder()
            .WithTitle($"{week.Level}")
            .WithFooter($"ID: {week.Id}")
            .WithDescription(board);
        await RespondAsync(embed: eb.Build(), ephemeral: secret);
    }
}