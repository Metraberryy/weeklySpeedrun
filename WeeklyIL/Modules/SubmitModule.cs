using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("submit", "Commands for submitting your times")]
public class SubmitModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public SubmitModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
    }
    
    [SlashCommand("video", "Submits a time with video proof")]
    public async Task WithVideo(string video, ulong? weekId = null)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        ulong subChannel = _dbContext.Guilds.First(g => g.Id == Context.Guild.Id).SubmissionsChannel;
        if (subChannel == 0)
        {
            await RespondAsync("No submission channel to submit to!", ephemeral: true);
            return;
        }

        weekId ??= _dbContext.CurrentWeek(Context.Guild.Id)?.Id ?? 0;
        WeekEntity? we = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == weekId);
        
        if (we == null || we.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            await RespondAsync("No week to submit to!", ephemeral: true);
            return;
        }

        if (!we.Ended && we.StartTimestamp < _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                .Where(w => w.StartTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .OrderBy(w => w.StartTimestamp).Last().StartTimestamp)
        {
            await RespondAsync("This week is currently not accepting submissions! Try again after the results are posted.", ephemeral: true);
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
            .Where(s => s.WeekId == weekId)
            .First(s => s.Video == video).Id;

        var cb = new ComponentBuilder()
            .WithButton("Verify", "verify_button", ButtonStyle.Success)
            .WithButton("Reject", "reject_button", ButtonStyle.Danger);

        var channel = (SocketTextChannel)await _client.GetChannelAsync(subChannel);
        await channel.SendMessageAsync($"ID: {id} | User: {Context.User.Username} | Week: {weekId} \nVideo: {video}", components: cb.Build());
        
        await RespondAsync("Video submitted! It will be timed and verified soon.", ephemeral: true);
    }
    
    [SlashCommand("blank", "Submits a blank time to the leaderboard - you won't have a time without proof")]
    public async Task NoVideo(ulong? weekId = null)
    {
        weekId ??= _dbContext.CurrentWeek(Context.Guild.Id)?.Id ?? 0;
        WeekEntity? we = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == weekId);
        
        if (we == null
            || (we.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds() 
                && !await _dbContext.UserIsOrganizer(Context)))
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
        
        SocketGuildUser? user = Context.Guild.GetUser(Context.User.Id);
        await user.AddRolesAsync(_dbContext.Guilds
            .Include(g => g.GameRoles)
            .First(g => g.Id == we.GuildId).GameRoles
            .Where(r => r.Game == we.Game)
            .Select(r => r.RoleId));
    }
}

