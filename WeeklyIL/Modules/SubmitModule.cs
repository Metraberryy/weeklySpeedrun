using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("submit", "Commands for submitting your times")]
public class SubmitModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public SubmitModule(WilDbContext dbContext, DiscordSocketClient client)
    {
        _dbContext = dbContext;
        _client = client;
    }
    
    [SlashCommand("video", "Submits a time with video proof")]
    public async Task WithVideo(string video, ulong? week = null)
    {
        await _dbContext.CreateIfNotExists(Context.Guild);
        ulong subChannel = _dbContext.Guilds.First(g => g.Id == Context.Guild.Id).SubmissionsChannel;
        if (subChannel == 0)
        {
            await RespondAsync("No submission channel to submit to!", ephemeral: true);
            return;
        }

        ulong? weekId = week ?? _dbContext.CurrentWeek(Context.Guild)?.Id;
        if (weekId == null || !_dbContext.Weeks.Where(w => w.GuildId == Context.Guild.Id).Any(w => w.Id == weekId))
        {
            await RespondAsync("No week to submit to!", ephemeral: true);
            return;
        }

        if (!(Uri.TryCreate(video, UriKind.Absolute, out Uri? result)
              && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps)))
        {
            await RespondAsync("Video is not a valid link!", ephemeral: true);
            return;
        }
        
        await _dbContext.Scores.AddAsync(new ScoreEntity
        {
            UserId = Context.User.Id,
            WeekId = (ulong)weekId,
            Video = video
        });
        await _dbContext.SaveChangesAsync();
        
        ulong id = _dbContext.Scores
            .Where(s => s.UserId == Context.User.Id)
            .Where(s => s.WeekId == (ulong)weekId)
            .First(s => s.Video == video).Id;

        var cb = new ComponentBuilder()
            .WithButton("Verify", "verify_button", ButtonStyle.Success)
            .WithButton("Reject", "reject_button", ButtonStyle.Danger);

        var channel = (ISocketMessageChannel)await _client.GetChannelAsync(subChannel);
        await channel.SendMessageAsync($"ID: {id} Video: {video}", components: cb.Build());
        
        await RespondAsync("Video submitted! It will be timed and verified soon.", ephemeral: true);
    }
    
    [SlashCommand("blank", "your did it")]
    public async Task NoVideo(ulong? week = null)
    {
        ulong? weekId = week ?? _dbContext.CurrentWeek(Context.Guild)?.Id;
        if (weekId == null || !_dbContext.Weeks.Where(w => w.GuildId == Context.Guild.Id).Any(w => w.Id == weekId))
        {
            await RespondAsync("No week to submit to!", ephemeral: true);
            return;
        }
        
        await _dbContext.Scores.AddAsync(new ScoreEntity
        {
            UserId = Context.User.Id,
            WeekId = (ulong)weekId,
            TimeMs = uint.MaxValue,
            Verified = true
        });
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync("Submitted without proof! You'll show up on the leaderboard without a time.", ephemeral: true);
    }
    
    
}

