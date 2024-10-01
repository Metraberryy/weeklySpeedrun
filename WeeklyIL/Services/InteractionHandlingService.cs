using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using WeeklyIL.Utility;

namespace WeeklyIL.Services;

public class InteractionHandlingService : IHostedService
{
    private readonly DiscordSocketClient _discord;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<InteractionService> _logger;

    public InteractionHandlingService(
        DiscordSocketClient discord,
        InteractionService interactions,
        IServiceProvider services,
        IConfiguration config,
        ILogger<InteractionService> logger)
    {
        _discord = discord;
        _interactions = interactions;
        _services = services;
        _config = config;
        _logger = logger;

        _interactions.Log += msg => LogHelper.OnLogAsync(_logger, msg);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discord.Ready += () => _interactions.RegisterCommandsGloballyAsync(true);
        _discord.InteractionCreated += OnInteractionAsync;

        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _interactions.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnInteractionAsync(SocketInteraction interaction)
    {
        if (interaction.IsDMInteraction)
        {
            await interaction.RespondAsync("nuh uh");
            return;
        }
        try
        {
            IResult? result;
            if (interaction is SocketMessageComponent component)
            {
                var context = new SocketInteractionContext<SocketMessageComponent>(_discord, component);
                result = await _interactions.ExecuteCommandAsync(context, _services);
            }
            else
            {
                var context = new SocketInteractionContext(_discord, interaction);
                result = await _interactions.ExecuteCommandAsync(context, _services);
            }
            
            if (!result.IsSuccess) await interaction.RespondAsync(result.ToString(), ephemeral: true);
        }
        catch
        {
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(msg => msg.Result.DeleteAsync());
            }
        }
    }
}