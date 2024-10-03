using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class VerifyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public VerifyModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
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
        
        WeekEntity week = _dbContext.Weeks.First(w => w.Id == score.WeekId);
        if (week.GuildId != Context.Guild.Id) return;

        score.TimeMs = (uint)time.TotalMilliseconds;
        score.Verified = true;
        await _dbContext.SaveChangesAsync();
        
        try
        {
            var channel = 
                (SocketTextChannel)await _client.GetChannelAsync(
                    _dbContext.Guild(Context.Guild.Id).AnnouncementsChannel);
            string mention = (await _client.GetUserAsync(score.UserId)).Mention;
            var ts = new TimeSpan((long)score.TimeMs * TimeSpan.TicksPerMillisecond);
            await channel.SendMessageAsync($@"{mention} got a time of `{ts:mm\:ss\.fff}` on {week.Level} !");
        } catch (Exception _) { /* ignored */ }

        await (await Context.Channel.GetMessageAsync(modal.MessageId)).DeleteAsync();
        await DeferAsync();
    }
    
    [ModalInteraction("reject_run", true)]
    public async Task RejectRun(RejectModal modal)
    {
        ScoreEntity? score = _dbContext.Scores.FirstOrDefault(s => s.Id == modal.RunId);
        if (score == null || _dbContext.Weeks.First(w => w.Id == score.WeekId).GuildId != Context.Guild.Id)
        {
            return;
        }

        try
        {
            await (await _client.GetUserAsync(score.UserId)).SendMessageAsync(
                $"Your run ({score.Video}) has been rejected. \nReason: {modal.Reason}");
        } catch (Exception _) { /* ignored */ }
        
        _dbContext.Remove(score);
        await _dbContext.SaveChangesAsync();

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