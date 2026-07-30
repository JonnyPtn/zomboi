using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

namespace zomboi;

public class InteractionHandler
{
    private readonly DiscordSocketClient m_client;
    private readonly InteractionService m_service;
    private readonly IServiceProvider m_provider;

    public InteractionHandler(DiscordSocketClient client, InteractionService service, IServiceProvider provider)
    {
        m_client = client;
        m_service = service;
        m_provider = provider;
    }

    public async Task InitializeAsync()
    {
        // Process when the client is ready, so we can register our commands.
        m_client.Ready += ReadyAsync;

        // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
        await m_service.AddModulesAsync(Assembly.GetEntryAssembly(), m_provider);

        // Process the InteractionCreated payloads to execute Interactions commands
        m_client.InteractionCreated += HandleInteraction;

        // ExecuteCommandAsync reports success whatever happens, so this is where failures show up
        m_service.InteractionExecuted += InteractionExecuted;
    }

    private async Task ReadyAsync()
    {
        try
        {
            await m_service.RegisterCommandsGloballyAsync();
        }
        catch (Exception exception)
        {
            // Usually means the bot was invited without the applications.commands scope
            DiscordError.Log("register the slash commands", exception);
        }
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
            var context = new SocketInteractionContext(m_client, interaction);

            await m_service.ExecuteCommandAsync(context, m_provider);
        }
        catch (Exception exception)
        {
            DiscordError.Log($"run {Describe(interaction)}", exception);

            // A failed command leaves its acknowledgement sitting there as though it had worked
            if (interaction.Type is InteractionType.ApplicationCommand)
            {
                try
                {
                    var response = await interaction.GetOriginalResponseAsync();
                    await response.DeleteAsync();
                }
                catch (Exception cleanup)
                {
                    Logger.Error($"Couldn't clear the response to {Describe(interaction)}: {cleanup.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Say what went wrong, in the bot's output and back to whoever ran the command.
    /// </summary>
    private static async Task InteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }
        Logger.Error($"Failed to run /{command.Name}: {result.Error} {result.ErrorReason}");

        var message = result.Error == InteractionCommandError.UnmetPrecondition
            ? result.ErrorReason
            : $"Something went wrong running /{command.Name}, check the bot's output for why";
        try
        {
            if (context.Interaction.HasResponded)
            {
                await context.Interaction.FollowupAsync(message, ephemeral: true);
            }
            else
            {
                await context.Interaction.RespondAsync(message, ephemeral: true);
            }
        }
        catch (Exception exception)
        {
            DiscordError.Log($"report the /{command.Name} failure", exception);
        }
    }

    private static string Describe(SocketInteraction interaction)
        => interaction is SocketSlashCommand command ? $"/{command.Data.Name}" : interaction.Type.ToString();
}
