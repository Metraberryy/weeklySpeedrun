using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Services;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class VerifyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;
    private readonly WeekEndTimers _weekEnder;

    public VerifyModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client, WeekEndTimers weekEnder)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
        _weekEnder = weekEnder;
    }
    
    public class VerifyModal : IModal
    {
        public string Title => "Verify run";
        
        [ModalTextInput("time")]
        public string Time { get; set; }
        
        [ModalTextInput("run_id")]
        public ulong RunId { get; set; }
        
        [ModalTextInput("message_id")]
        public ulong MessageId { get; set; }
    }
    
    public class RejectModal : IModal
    {
        public string Title => "Reject run";
        
        [ModalTextInput("reason")]
        public string Reason { get; set; }
        
        [ModalTextInput("run_id")]
        public ulong RunId { get; set; }
        
        [ModalTextInput("message_id")]
        public ulong MessageId { get; set; }
    }
    
    [ModalInteraction("verify_run", true)]
    public async Task VerifyRun(VerifyModal modal)
    {
        bool cont = TimeSpan.TryParseExact(
            modal.Time, @"m\:ss\.fff", CultureInfo.InvariantCulture,
            out TimeSpan time);

        if (!cont)
        {
            return;
        }

        ScoreEntity? score = _dbContext.Scores.FirstOrDefault(s => s.Id == modal.RunId);
        if (score == null) return;

        if (score.Verified)
        {
            await RespondAsync("The run has already been verified.", ephemeral: true);
            return;
        }
        
        WeekEntity week = _dbContext.Week(score.WeekId);
        if (week.GuildId != Context.Guild.Id) return;

        score.TimeMs = (uint)time.TotalMilliseconds;
        score.Verified = true;
        await _dbContext.SaveChangesAsync();

        // try to end the week if its waiting for verifications
        if (!week.Ended && week.StartTimestamp < _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                .Where(w => w.StartTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .OrderBy(w => w.StartTimestamp).Last().StartTimestamp) await _weekEnder.TryEndWeek(week);

        try
        {
            var channel =
                (SocketTextChannel)await _client.GetChannelAsync(
                    _dbContext.Guild(week.GuildId).AnnouncementsChannel);

            var ts = new TimeSpan((long)score.TimeMs * TimeSpan.TicksPerMillisecond);

            string level = week.Level;
            Uri? uri = level.GetUriFromString();
            if (uri != null)
            {
                level = level.Replace(uri.OriginalString, $"<{uri.OriginalString}>");
            }

            uint place = (uint)(_dbContext.Scores
                .Where(s => s.WeekId == score.WeekId)
                .Where(s => s.Verified)
                .GroupBy(s => s.UserId)
                .Select(g => g.OrderBy(s => s.TimeMs).First()).AsEnumerable()
                .OrderBy(s => s.TimeMs).ToList()
                .IndexOf(score) + 1);


            await Context.Guild.DownloadUsersAsync();
            SocketGuildUser? user = Context.Guild.GetUser(score.UserId);

            if (place != 0) // is pb
            {
                string placeStr = place.ToString();
                if (placeStr.Length > 1 && placeStr[^2] == '1')
                {
                    placeStr += "th";
                }
                else
                {
                    placeStr += placeStr.Last() switch
                    {
                        '1' => "st",
                        '2' => "nd",
                        '3' => "rd",
                        _ => "th"
                    };
                }
                bool isCurrent = week.Id == _dbContext.CurrentWeek(week.GuildId)?.Id;
                placeStr = !isCurrent || week.ShowVideo ? $"[{placeStr} place PB]({score.Video})" : $"{placeStr} place PB";
                await channel.SendMessageAsync($@"{user.Mention} got a {placeStr} with a time of `{ts:mm\:ss\.fff}` on {level}!");
            }
                

            await user.AddRolesAsync(_dbContext.Guilds
                .Include(g => g.GameRoles)
                .First(g => g.Id == week.GuildId).GameRoles
                .Where(r => r.Game == week.Game)
                .Select(r => r.RoleId));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        await (await Context.Channel.GetMessageAsync(modal.MessageId)).DeleteAsync();
        await DeferAsync();
    }
    
    [ModalInteraction("reject_run", true)]
    public async Task RejectRun(RejectModal modal)
    {
        ScoreEntity? score = _dbContext.Scores.FirstOrDefault(s => s.Id == modal.RunId);
        if (score == null) return;
        
        WeekEntity week = _dbContext.Week(score.WeekId);
        if (week.GuildId != Context.Guild.Id) return;

        try
        {
            await (await _client.GetUserAsync(score.UserId)).SendMessageAsync(
                $"Your run ({score.Video}) has been rejected. \nReason: {modal.Reason}");
        } catch (Exception _) { /* ignored */ }
        
        _dbContext.Remove(score);
        await _dbContext.SaveChangesAsync();
        
        // try to end the week if its waiting for verifications
        if (!week.Ended && week.StartTimestamp < _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                .Where(w => w.StartTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .OrderBy(w => w.StartTimestamp).Last().StartTimestamp) await _weekEnder.TryEndWeek(week);

        await (await Context.Channel.GetMessageAsync(modal.MessageId)).DeleteAsync();
        await DeferAsync();
    }
}

public class VerifyComponentInteractions : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    [ComponentInteraction("verify_button", true)]
    public async Task VerifyButton()
    {
        string id = Context.Interaction.Message.Content.Split(' ')[1];
        
        // using a modalbuilder here to automatically set the run id
        var mb = new ModalBuilder()
            .WithCustomId("verify_run")
            .WithTitle("Verify run")
            .AddTextInput("Time", "time", placeholder: "mm:ss.fff")
            .AddTextInput("do not edit", "run_id", value: id)
            .AddTextInput("do not edit", "message_id", value: Context.Interaction.Message.Id.ToString());

        await Context.Interaction.RespondWithModalAsync(mb.Build());
    }
    
    [ComponentInteraction("reject_button", true)]
    public async Task RejectButton()
    {
        string id = Context.Interaction.Message.Content.Split(' ')[1];
        
        // using a modalbuilder here to automatically set the run id
        var mb = new ModalBuilder()
            .WithCustomId("reject_run")
            .WithTitle("Reject run")
            .AddTextInput("Reason", "reason", placeholder: "a very good reason to reject the run")
            .AddTextInput("do not edit", "run_id", value: id)
            .AddTextInput("do not edit", "message_id", value: Context.Interaction.Message.Id.ToString());

        await Context.Interaction.RespondWithModalAsync(mb.Build());
    }
}