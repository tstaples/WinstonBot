﻿using Discord;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    public class CommandNameDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (CommandInfo command in CommandHandler.CommandEntries.Values)
            {
                // TODO: at least use a constant or something
                if (command.Name != "configure")
                {
                    builder.AddChoice(command.Name, command.Name);
                }
            }
        }
    }

    public class CommandsWithActionsDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (CommandInfo command in CommandHandler.CommandEntries.Values)
            {
                // TODO: at least use a constant or something
                if (command.Name != "configure" && command.Actions != null)
                {
                    builder.AddChoice(command.Name, command.Name);
                }
            }
        }
    }
}
