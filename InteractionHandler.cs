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
    }

    private async Task ReadyAsync()
    {
        await m_service.RegisterCommandsGloballyAsync();
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
            var context = new SocketInteractionContext(m_client, interaction);

            // Execute the incoming command.
            var result = await m_service.ExecuteCommandAsync(context, m_provider);

            // Due to async nature of InteractionFramework, the result here may always be success.
            // That's why we also need to handle the InteractionExecuted event.
            if (!result.IsSuccess)
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        // implement
                        break;
                    default:
                        break;
                }
        }
        catch
        {
            // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }
}