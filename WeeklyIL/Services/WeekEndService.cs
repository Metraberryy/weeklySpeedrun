using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WeeklyIL.Database;

namespace WeeklyIL.Services;

public class WeekEndService : IHostedService
{
    private readonly WilDbContext _dbContext;
    private readonly WeekEndTimers _timers;

    public WeekEndService(IDbContextFactory<WilDbContext> contextFactory, WeekEndTimers timers, DiscordSocketClient _)
    {
        _dbContext = contextFactory.CreateDbContext();
        _timers = timers;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (GuildEntity guild in _dbContext.Guilds)
        {
            var weeks = _dbContext.Weeks
                .Where(w => w.GuildId == guild.Id).AsEnumerable()
                .OrderBy(w => w.StartTimestamp).ToList();
            for (int i = 0; i < weeks.Count - 1; i++)
            {
                WeekEntity week = weeks[i];
                if (week.Ended) continue;
                
                WeekEntity nextWeek = weeks[i + 1];
            
                if (nextWeek.StartTimestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    _timers.OnWeekEnd(week);
                }
            }
            await _timers.UpdateGuildTimer(guild.Id);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _timers.Timers)
        {
            await kvp.Value.DisposeAsync();
        }
    }
}