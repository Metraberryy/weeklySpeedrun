using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Types;

namespace WeeklyIL.Modules;

public class VerifyComponentInteractions : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    public static readonly Dictionary<ulong, VerifyInteractionContext> Interactions = [];
    
    [ComponentInteraction("verify_button", true)]
    public async Task VerifyButton()
    {
        var context = new VerifyInteractionContext
        {
            ScoreId = ulong.Parse(Context.Interaction.Message.Content.Split(' ')[1]),
            MessageId = Context.Interaction.Message.Id
        };
        Interactions[Context.Interaction.User.Id] = context;

        await Context.Interaction.RespondWithModalAsync<VerifyModule.VerifyModal>("verify_run");
    }
    
    [ComponentInteraction("reject_button", true)]
    public async Task RejectButton()
    {
        var context = new VerifyInteractionContext
        {
            ScoreId = ulong.Parse(Context.Interaction.Message.Content.Split(' ')[1]),
            MessageId = Context.Interaction.Message.Id
        };
        Interactions[Context.Interaction.User.Id] = context;

        await Context.Interaction.RespondWithModalAsync<VerifyModule.RejectModal>("reject_run");
    }
}