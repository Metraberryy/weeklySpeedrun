using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;

namespace WeeklyIL.Modules;

public class VerifyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public VerifyModule(WilDbContext dbContext, DiscordSocketClient client)
    {
        _dbContext = dbContext;
        _client = client;
    }
    
    public class VerifyModal : IModal
    {
        public string Title => "Verify run";
        
        [ModalTextInput("time", placeholder: "mm:ss.fff")]
        public string Time { get; set; }
        
        [ModalTextInput("run_id")]
        public ulong RunId { get; set; }
        
        [ModalTextInput("message_id")]
        public ulong MessageId { get; set; }
    }
    
    [ModalInteraction("verify_run", true)]
    public async Task VerifyRun(VerifyModal modal)
    {
        ulong id = modal.RunId;
        bool cont = TimeSpan.TryParseExact(
            modal.Time, @"m\:ss\.fff", CultureInfo.InvariantCulture,
            out TimeSpan time);

        if (!cont)
        {
            return;
        }

        ScoreEntity? score = _dbContext.Scores.FirstOrDefault(s => s.Id == id);
        if (score == null || _dbContext.Weeks.First(w => w.Id == score.WeekId).GuildId != Context.Guild.Id)
        {
            return;
        }

        score.TimeMs = (uint)time.TotalMilliseconds;
        score.Verified = true;
        await _dbContext.SaveChangesAsync();

        await (await Context.Channel.GetMessageAsync(modal.MessageId)).DeleteAsync();
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
}