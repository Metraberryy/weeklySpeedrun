using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("weeks", "Commands for managing weeks")]
public class WeekModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public WeekModule(WilDbContext dbContext, DiscordSocketClient client)
    {
        _dbContext = dbContext;
        _client = client;
    }

    private async Task<bool> PermissionsFail()
    {
        if (await _dbContext.UserIsOrganizer(Context))
        {
            return false;
        }

        await RespondAsync("You can't do that here!", ephemeral: true);
        return true;
    }

    [SlashCommand("queue", "Shows the queue of upcoming weeks")]
    public async Task WeeksQueue()
    {
        if (await PermissionsFail())
        {
            return;
        }

        var eb = new EmbedBuilder().WithTitle("Upcoming weeks");
        foreach (WeekEntity week in _dbContext.Weeks
                     .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                     .Where(w => w.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                     .OrderBy(w => w.StartTimestamp))
        {
            eb.AddField($"<t:{week.StartTimestamp}:D>", week.Level);
        }
        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }
    
    [SlashCommand("past", "Shows previous weeks")]
    public async Task PastWeeks()
    {
        if (await PermissionsFail())
        {
            return;
        }

        var eb = new EmbedBuilder().WithTitle("Previous weeks");
        foreach (WeekEntity week in _dbContext.Weeks
                     .Where(w => w.GuildId == Context.Guild.Id).AsEnumerable()
                     .Where(w => w.StartTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                     .OrderByDescending(w => w.StartTimestamp))
        {
            eb.AddField($"<t:{week.StartTimestamp}:D>", week.Level);
        }
        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }
    
    [SlashCommand("new", "Create a new week and add it to the queue")]
    public async Task NewWeek()
    {
        if (await PermissionsFail())
        {
            return;
        }
        
        WeekEntity? week = _dbContext.Weeks.OrderBy(w => w.StartTimestamp).LastOrDefault(w => w.GuildId == Context.Guild.Id);
        if (week == null)
        {
            // no weeks exist! bring up the first week modal
            await RespondWithModalAsync<FirstWeekModal>("first_week");
            return;
        }
        await RespondWithModalAsync<NewWeekModal>("new_week");
    }
    
    public class FirstWeekModal : IModal
    {
        public string Title => "First-time setup";
        
        [InputLabel("Start of the first week as a unix timestamp")]
        [ModalTextInput("timestamp", TextInputStyle.Short, "1727601767")]
        public uint Timestamp { get; set; }
        
        [InputLabel("What are we running?")]
        [ModalTextInput("level_name", TextInputStyle.Short, "https://beacon.lbpunion.com/slot/17962/getting-over-it-14-players")]
        public string Level { get; set; }
    }
    [ModalInteraction("first_week", true)]
    public async Task FirstWeekResponse(FirstWeekModal modal)
    {
        if (modal.Timestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            // silently fail so the modal can be resubmitted
            return;
        }
        
        await CreateWeek(modal.Timestamp, modal.Level);
    }

    public class NewWeekModal : IModal
    {
        public string Title => "New week";
        
        [InputLabel("What are we running?")]
        [ModalTextInput("level_name", TextInputStyle.Short, "https://beacon.lbpunion.com/slot/17962/getting-over-it-14-players")]
        public string Level { get; set; }
    }
    [ModalInteraction("new_week", true)]
    public async Task NewWeekResponse(NewWeekModal modal)
    {
        uint time = _dbContext.Weeks.OrderByDescending(w => w.StartTimestamp).First(w => w.GuildId == Context.Guild.Id).StartTimestamp + 604800;
        await CreateWeek(time, modal.Level);
    }

    private async Task CreateWeek(uint time, string level)
    {
        await _dbContext.AddAsync(new WeekEntity
        {
            GuildId = Context.Guild.Id,
            StartTimestamp = time,
            Level = level
        });
        await _dbContext.SaveChangesAsync();
        ulong id = _dbContext.Weeks.First(w => w.StartTimestamp == time && w.GuildId == Context.Guild.Id).Id;
        
        await RespondAsync(
            $"Week created on <t:{time}:f>! `id: {id}`\n" +
            $"If you need to edit this time, you can change it with `/weeks time {id} <unix timestamp>`",
            ephemeral: true);
    }
    
    [SlashCommand("time", "Sets the time of a week by id")]
    public async Task SetTime(ulong id, uint timestamp)
    {
        if (await PermissionsFail())
        {
            return;
        }
        
        WeekEntity? week = _dbContext.Weeks.FirstOrDefault(w => w.Id == id);
        if (week == null || week.GuildId != Context.Guild.Id)
        {
            await RespondAsync(
                $"No week found with id `{id}`.",
                ephemeral: true);
            return;
        }
        if (timestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            await RespondAsync(
                $"<t:{timestamp}:f> is before the current time!",
                ephemeral: true);
            return;
        }

        week.StartTimestamp = timestamp;
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync(
            $"Week `{id}` will now be on <t:{timestamp}:f>.",
            ephemeral: true);
    }
}