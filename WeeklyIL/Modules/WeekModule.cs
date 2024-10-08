using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Services;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("week", "Commands for managing weeks")]
public class WeekModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext;
    private readonly DiscordSocketClient _client;
    private readonly WeekEndTimers _weekEnder;

    public WeekModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client, WeekEndTimers weekEnder)
    {
        _dbContext = contextFactory.CreateDbContext();
        _client = client;
        _weekEnder = weekEnder;
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
        public string Title => "First week";
        
        [InputLabel("Start of the first week as a unix timestamp")]
        [ModalTextInput("timestamp", placeholder: "1727601767")]
        public uint Timestamp { get; set; }
        
        [InputLabel("What are we running?")]
        [ModalTextInput("level_name", placeholder: "https://beacon.lbpunion.com/slot/17962/getting-over-it-14-players")]
        public string Level { get; set; }
        
        [InputLabel("What game?")]
        [ModalTextInput("game_name", placeholder: "Exactly how it's written in the /role game command")]
        public string Game { get; set; }
        
        [InputLabel("Show videos before the week is over?")]
        [ModalTextInput("show_video", placeholder: "True/False")]
        public string ShowVideo { get; set; }
    }
    [ModalInteraction("first_week", true)]
    public async Task FirstWeekResponse(FirstWeekModal modal)
    {
        if (modal.Timestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;
        if (_dbContext.Guilds.Include(g => g.GameRoles)
            .First(g => g.Id == Context.Guild.Id)
            .GameRoles.All(r => r.Game != modal.Game)) return;
        
        await CreateWeek(modal.Timestamp, modal.Level, modal.Game, bool.Parse(modal.ShowVideo));
    }

    public class NewWeekModal : IModal
    {
        public string Title => "New week";
        
        [InputLabel("What are we running?")]
        [ModalTextInput("level_name", placeholder: "https://beacon.lbpunion.com/slot/17962/getting-over-it-14-players")]
        public string Level { get; set; }
        
        [InputLabel("What game?")]
        [ModalTextInput("game_name", placeholder: "Exactly how it's written in the /role game command")]
        public string Game { get; set; }
        
        [InputLabel("Show videos before the week is over?")]
        [ModalTextInput("show_video", placeholder: "True/False")]
        public string ShowVideo { get; set; }
    }
    [ModalInteraction("new_week", true)]
    public async Task NewWeekResponse(NewWeekModal modal)
    {
        if (_dbContext.Guilds.Include(g => g.GameRoles)
            .First(g => g.Id == Context.Guild.Id)
            .GameRoles.All(r => r.Game != modal.Game)) return;
        uint time = _dbContext.Weeks.OrderByDescending(w => w.StartTimestamp).First(w => w.GuildId == Context.Guild.Id).StartTimestamp + 604800;
        await CreateWeek(time, modal.Level, modal.Game, bool.Parse(modal.ShowVideo));
    }

    private async Task CreateWeek(uint time, string level, string game, bool showVideo)
    {
        var entry = await _dbContext.AddAsync(new WeekEntity
        {
            GuildId = Context.Guild.Id,
            StartTimestamp = time,
            Level = level,
            Game = game,
            ShowVideo = showVideo
        });
        await _dbContext.SaveChangesAsync();

        // update timer in case this is the next week
        await _weekEnder.UpdateGuildTimer(Context.Guild.Id);
        
        await RespondAsync(
            $"Week created on <t:{time}:f>! `id: {entry.Entity.Id}`\n" +
            $"You can edit this with `/week edit {entry.Entity.Id}`.",
            ephemeral: true);
    }
    
    [SlashCommand("edit", "Edit a week by id")]
    public async Task EditWeek(ulong id)
    {
        if (await PermissionsFail())
        {
            return;
        }

        WeekEntity? week = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == id);
        
        if (week == null)
        {
            await RespondAsync("Week doesn't exist!");
            return;
        }
        
        // using a modalbuilder here to automatically set the current things
        var mb = new ModalBuilder()
            .WithCustomId("edit_week")
            .WithTitle("Edit week")
            .AddTextInput("Start of the week as a unix timestamp", "timestamp", placeholder: "1727601767", value: week.StartTimestamp.ToString())
            .AddTextInput("What are we running?", "level_name", placeholder: "https://beacon.lbpunion.com/slot/17962/getting-over-it-14-players", value: week.Level)
            .AddTextInput("What game?", "game_name", placeholder: "Exactly how it's written in the /role game command", value: week.Game)
            .AddTextInput("Show videos before the week is over?", "show_video", placeholder: "True/False", value: week.ShowVideo.ToString())
            .AddTextInput("ID", "week_id", value: id.ToString());

        await RespondWithModalAsync(mb.Build());
    }
    
    public class EditWeekModal : IModal
    {
        public string Title => "Edit week";
        
        [ModalTextInput("timestamp")]
        public uint Timestamp { get; set; }
        
        [ModalTextInput("level_name")]
        public string Level { get; set; }
        
        [ModalTextInput("game_name")]
        public string Game { get; set; }
        
        [ModalTextInput("week_id")]
        public ulong WeekId { get; set; }
        
        [ModalTextInput("show_video")]
        public string ShowVideo { get; set; }
    }
    [ModalInteraction("edit_week", true)]
    public async Task EditWeekResponse(EditWeekModal modal)
    {
        WeekEntity week = _dbContext.Week(modal.WeekId);
        if (week.GuildId != Context.Guild.Id) return;
        if (_dbContext.Guilds.Include(g => g.GameRoles)
            .First(g => g.Id == Context.Guild.Id)
            .GameRoles.All(r => r.Game != modal.Game)) return;
        
        _dbContext.Weeks.Update(week);
        week.StartTimestamp = modal.Timestamp;
        week.Level = modal.Level;
        week.Game = modal.Game;
        week.ShowVideo = bool.Parse(modal.ShowVideo);
        
        await _dbContext.SaveChangesAsync();

        await _weekEnder.UpdateGuildTimer(Context.Guild.Id);

        await RespondAsync($"Successfully edited week {modal.WeekId}!", ephemeral: true);
    }
}