﻿using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;
using WinstonBot.Attributes;

namespace WinstonBot.Commands.Config
{
    [SubCommand(Name = "action", ParentCommand = typeof(ConfigCommand))]
    internal class ConfigureActionSubCommand : ISubCommand
    {
        public string Name => "action";

        private List<ISubCommand> _subCommands = new()
        {
            new AddRoleOperation(),
            new RemoveRoleOperation(),
            new ViewRolesOperation()
        };

        public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new ConfigCommandContext(client, arg, services);
        }

        public SlashCommandOptionBuilder Build()
        {
            var actionCommandGroup = new SlashCommandOptionBuilder()
                .WithName("action")
                .WithDescription("Configure command action permissions")
                .WithRequired(false)
                .WithType(ApplicationCommandOptionType.SubCommandGroup);

            foreach (ISubCommand subCommand in _subCommands)
            {
                actionCommandGroup.AddOption(subCommand.Build());
            }

            return actionCommandGroup;
        }

        public async Task HandleCommand(CommandContext commandContext)
        {
            //var context = (ConfigCommandContext)commandContext;
            //List<SocketSlashCommandDataOption> options = new();//temp
            //if (options == null)
            //{
            //    Console.WriteLine($"Expected valid options for subcommand: {Name}");
            //    return;
            //}

            //string subCommandName = (string)options.First().Value;
            //foreach (ISubCommand subCommand in _subCommands)
            //{
            //    if (subCommand.Name == subCommandName)
            //    {
            //        await subCommand.HandleCommand(context);
            //        return;
            //    }
            //}
        }

        private static void GetActionRoles(ConfigService configService, ulong guildId, string commandName, string actionName, out List<ulong> roles)
        {
            var entries = configService.Configuration.GuildEntries;
            if (!entries.ContainsKey(guildId))
            {
                entries.TryAdd(guildId, new GuildEntry());
            }

            var commandEntries = entries[guildId].Commands;
            if (!commandEntries.ContainsKey(commandName))
            {
                commandEntries.Add(commandName, new CommandEntry());
            }

            var commandEntry = commandEntries[commandName];
            if (!commandEntry.ActionRoles.ContainsKey(actionName))
            {
                commandEntry.ActionRoles.Add(actionName, new List<ulong>());
            }

            roles = commandEntry.ActionRoles[actionName];
        }

        [SubCommand(Name = "add-role", ParentCommand = typeof(ConfigureActionSubCommand))]
        private class AddRoleOperation : ISubCommand
        {
            public string Name => "add-role";

            [CommandOption("command")]
            public string TargetCommand { get; set; }

            [CommandOption("action")]
            public string TargetAction { get; set; }

            [CommandOption("role")]
            public SocketRole TargetRole { get; set; }

            public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public SlashCommandOptionBuilder Build()
            {
                return new SlashCommandOptionBuilder();
            }

            public async Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                if (TargetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.SlashCommand.RespondAsync($"Cannot add {context.Guild.EveryoneRole.Mention} to commands as it is the default.\n" +
                        $"To set a command to {context.Guild.EveryoneRole.Mention}, remove all roles for it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                List<ulong> actionRoles;
                GetActionRoles(configService, context.Guild.Id, TargetCommand, TargetAction, out actionRoles);
                if (Utility.AddUnique(actionRoles, TargetRole.Id))
                {
                    configService.UpdateConfig(configService.Configuration);
                    await context.SlashCommand.RespondAsync($"Added role {TargetRole.Mention} to {TargetCommand}:{TargetAction}", ephemeral: true);
                }
                else
                {
                    await context.SlashCommand.RespondAsync($"{TargetCommand}:{TargetAction} already contains role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand(Name = "remove-role", ParentCommand = typeof(ConfigureActionSubCommand))]
        private class RemoveRoleOperation : ISubCommand
        {
            public string Name => "remove-role";

            [CommandOption("command")]
            public string TargetCommand { get; set; }

            [CommandOption("action")]
            public string TargetAction { get; set; }

            [CommandOption("role")]
            public SocketRole TargetRole { get; set; }

            public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public SlashCommandOptionBuilder Build()
            {
                return new SlashCommandOptionBuilder();
            }

            public async Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                if (TargetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.SlashCommand.RespondAsync($"Cannot remove {context.Guild.EveryoneRole.Mention} from commands as it is the default.\n" +
                        $"To make a command not available to {context.Guild.EveryoneRole.Mention}, add additional roles to it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                List<ulong> actionRoles;
                GetActionRoles(configService, context.Guild.Id, TargetCommand, TargetAction, out actionRoles);
                if (actionRoles.Contains(TargetRole.Id))
                {
                    actionRoles.Remove(TargetRole.Id);
                    configService.UpdateConfig(configService.Configuration);
                    await context.SlashCommand.RespondAsync($"Removed role {TargetRole.Mention} from {TargetCommand}:{TargetAction}", ephemeral: true);
                }
                else
                {
                    await context.SlashCommand.RespondAsync($"{TargetCommand}:{TargetAction} doesn't contain role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand(Name = "view-roles", ParentCommand = typeof(ConfigureActionSubCommand))]
        private class ViewRolesOperation : ISubCommand
        {
            public string Name => "view-roles";

            [CommandOption("command")]
            public string TargetCommand { get; set; }

            [CommandOption("action")]
            public string TargetAction { get; set; }

            public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public SlashCommandOptionBuilder Build()
            {
                return new SlashCommandOptionBuilder();
            }

            public async Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;

                List<ulong> actionRoles;
                GetActionRoles(context.ConfigService, context.Guild.Id, TargetCommand, TargetAction, out actionRoles);
                if (actionRoles.Count > 0)
                {
                    await context.SlashCommand.RespondAsync($"Roles for {TargetCommand}:{TargetAction}: \n{Utility.JoinRoleMentions(context.Guild, actionRoles)}", ephemeral: true);
                }
                else
                {
                    await context.SlashCommand.RespondAsync($"Roles for {TargetCommand}:{TargetAction}: \n{context.Guild.EveryoneRole.Mention}", ephemeral: true);
                }
            }
        }
    }
}
