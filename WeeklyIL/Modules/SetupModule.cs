using Discord;
using Discord.Interactions;
using WeeklyIL.Database;

namespace WeeklyIL.Modules;

public class SetupModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;

    public SetupModule(WilDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    [SlashCommand("new-week", "Create a new week and add it to the queue")]
    public async Task NewWeek()
    {
        WeekEntity? week = _dbContext.Weeks.OrderBy(w => w.StartTimestamp).LastOrDefault(w => w.GuildId == Context.Guild.Id);
        if (week == null)
        {
            // the guild hasn't been added to the db yet, lets set that up
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
    [ModalInteraction("first_week")]
    public async Task FirstWeekResponse(FirstWeekModal modal)
    {
        if (modal.Timestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            // silently fail so the modal can be resubmitted
            return;
        }
        
        // create the week entity
        await _dbContext.AddAsync(new WeekEntity
        {
            GuildId = Context.Guild.Id,
            StartTimestamp = modal.Timestamp,
            Level = modal.Level
        });
        await _dbContext.SaveChangesAsync();
        ulong id = _dbContext.Weeks.First(w => w.StartTimestamp == modal.Timestamp && w.GuildId == Context.Guild.Id).Id;
        
        await RespondAsync(
            $"Week created on <t:{modal.Timestamp}:f>! `id: {id}`\n" +
            $"If you need to edit this time, you can change it with `/set-time {id} <unix timestamp>`",
            ephemeral: true);
    }

    public class NewWeekModal : IModal
    {
        public string Title => "New week";
        
        [InputLabel("What are we running?")]
        [ModalTextInput("level_name", TextInputStyle.Short, "https://beacon.lbpunion.com/slot/17962/getting-over-it-14-players")]
        public string Level { get; set; }
    }
    [ModalInteraction("new_week")]
    public async Task NewWeekResponse(NewWeekModal modal)
    {
        uint time = _dbContext.Weeks.OrderBy(w => w.StartTimestamp).Last(w => w.GuildId == Context.Guild.Id).StartTimestamp + 604800;
        
        // create the week entity
        await _dbContext.AddAsync(new WeekEntity
        {
            GuildId = Context.Guild.Id,
            StartTimestamp = time,
            Level = modal.Level
        });
        await _dbContext.SaveChangesAsync();
        ulong id = _dbContext.Weeks.First(w => w.StartTimestamp == time && w.GuildId == Context.Guild.Id).Id;
        
        await RespondAsync(
            $"Week created on <t:{time}:f>! `id: {id}`\n" +
            $"If you need to edit this time, you can change it with `/set-time {id} <unix timestamp>`",
            ephemeral: true);
    }
    
    [SlashCommand("set-time", "Sets the time of a week by id")]
    public async Task SetTime(ulong id, uint timestamp)
    {
        WeekEntity? week = _dbContext.Weeks.FirstOrDefault(w => w.Id == id);
        if (week == null)
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
        await RespondAsync(
            $"Week `{id}` will now be on <t:{timestamp}:f>.",
            ephemeral: true);
    }
}