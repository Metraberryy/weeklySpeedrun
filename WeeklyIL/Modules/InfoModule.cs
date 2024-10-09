using Discord.Interactions;

namespace WeeklyIL.Modules;

public class InfoModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("info", "Shows info about the bot")]
    public async Task Info() =>
        await RespondAsync(
            "# :information_source: Information\n\n" +
            
            "## Every week we choose a new level/short category to run\n\n" +
            
            "- View the current week's leaderboard with `/view week`\n" +
            "- Submit your times with `/submit video`\n" +
            "- Want to submit but can't record a video? Submit with `/submit blank`\n" +
            "- Get a runner role for the relevant game when your run is verified!\n" +
            "- Get a stacked \"Weekly Winner\" role for being #1 when the week ends!\n\n" +
            
            "## Weeks will be grouped at the end of a month\n\n" +
            
            "- View the current month's leaderboard with `/view month`\n" +
            "  - Times are a combination of your current time in every week of the month\n" +
            "- Get a unique themed role for being #1 when the month ends!\n\n" +
            
            "## Live the dream with a time machine\n\n" +
            
            "- See previous weeks with `/past weeks` (`/past months` for months)\n" +
            "- Add a week ID parameter to any relevant command to make it apply to the specified week\n" +
            "  - Ditto for months\n\n" +
            
            "## Statistics\n\n" +
            
            "- Use `/stats` to see your stats!\n" +
            "  - Add a user parameter to see the mentioned user's stats", ephemeral: true);
}