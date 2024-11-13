using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WeeklyIL.Database;

namespace WeeklyIL.Services;

public class WeekEndService : IHostedService
{
    private readonly WilDbContext _dbContext;
    private readonly WeekEndTimers _timers;

    public WeekEndService(IDbContextFactory<WilDbContext> contextFactory, WeekEndTimers timers, DiscordSocketClient client)
    {
        _dbContext = contextFactory.CreateDbContext();
        _timers = timers;

        client.Ready += Ready;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // yeah man
    }

    private async Task Ready()
    {
        foreach (GuildEntity guild in _dbContext.Guilds)
        {
            // this was a failed attempt to go back and end weeks if the bot was down when they were supposed to end
            // hopefully it wont be an issue anyway (foreshadowing)
            /*var weeks = _dbContext.Weeks
                .Where(w => w.GuildId == guild.Id).AsEnumerable()
                .OrderBy(w => w.StartTimestamp).ToList();
            for (int i = 0; i < weeks.Count - 1; i++)
            {
                WeekEntity week = weeks[i];
                if (week.Ended) continue;

                WeekEntity nextWeek = weeks[i + 1];

                if (nextWeek.StartTimestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    await _timers.TryEndWeek(week);
                }
            }*/
            await _timers.UpdateGuildTimer(guild.Id);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _timers.DisposeAsync();
    }
}