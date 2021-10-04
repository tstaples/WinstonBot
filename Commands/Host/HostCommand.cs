using Discord;
using WinstonBot.Data;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace WinstonBot.Commands
{
    public class HostPvmSignup : ICommand
    {
        public string Name => "host-pvm-signup";
        public ICommand.Permission DefaultPermission => ICommand.Permission.Everyone;
        public ulong AppCommandId { get; set; }
        public IEnumerable<IAction> Actions => _actions;

        private List<IAction> _actions = new List<IAction>()
        {
            new SignupAction(),
            new QuitAction(),
            new CompleteTeamAction(),
            new ConfirmTeamAction(),
            new EditCompletedTeamAction(),
            new CancelTeamConfirmationAction(),
            new AddUserToTeamAction(),
            new RemoveUserFromTeamAction()
        };

        // we only have the mention string in the desc.
        private List<string> testNames = new List<string>()
        {
            { "<@141439679890325504>" },
            { "<@204793753691619330>" },
            { "<@889961722314637342>" },
            { "<@879404492922167346>" },
            { "<@856679611899576360>" }
        };

        private ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> _originalSignupsForMessage = new();
        private ConcurrentDictionary<ulong, bool> _messagesBeingEdited = new();

        public SlashCommandProperties BuildCommand()
        {
            var choices = new SlashCommandOptionBuilder()
                    .WithName("boss")
                    .WithDescription("The boss to host")
                    .WithRequired(true)
                    .WithType(ApplicationCommandOptionType.Integer);

            foreach (var entry in BossData.Entries)
            {
                choices.AddChoice(entry.CommandName, (int)entry.Id);
            }

            var hostQueuedCommand = new SlashCommandBuilder()
                .WithName(Name)
                .WithDescription("Create a signup for a pvm event")
                .AddOption(choices)
                .AddOption("message", ApplicationCommandOptionType.String, "Additional info about the event to be added to the message body.", required: false);

            return hostQueuedCommand.Build();
        }

        public async Task HandleCommand(Commands.CommandContext context)
        {
            var slashCommand = context.SlashCommand;

            var bossIndex = (long)slashCommand.Data.Options.First().Value;
            if (!BossData.ValidBossIndex(bossIndex))
            {
                await slashCommand.RespondAsync($"Invalid boss index {bossIndex}. Max Index is {(long)BossData.Boss.Count - 1}", ephemeral: true);
                return;
            }

            var bossPrettyName = BossData.Entries[bossIndex].PrettyName;
            string message = $"Sign up for {bossPrettyName}"; // default message
            if (slashCommand.Data.Options.Count > 1)
            {
                message = (string)slashCommand.Data.Options.ElementAt(1).Value;
            }

            var buttons = HostHelpers.BuildSignupButtons(bossIndex);
            var embed = HostHelpers.BuildSignupEmbed(bossIndex, testNames);

            await slashCommand.RespondAsync(message, embed: embed, component: buttons, allowedMentions: AllowedMentions.All);
        }

        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            return new HostActionContext(client, arg, services, _originalSignupsForMessage, _messagesBeingEdited);
        }
    }
}
