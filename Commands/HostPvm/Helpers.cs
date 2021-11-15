using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;

namespace WinstonBot.Commands.HostPvm
{
    internal static class Helpers
    {
        public static void BuildSignup(IEnumerable<RuntimeRole> roles, BossData.Entry entry, SocketGuild guild, out Embed embed, out MessageComponent component)
        {
            var builder = new EmbedBuilder()
                .WithTitle(entry.PrettyName);

            var componentBuilder = new ComponentBuilder();
            foreach (RuntimeRole role in roles)
            {
                string value = "Empty";
                if (role.Users.Count > 0)
                {
                    value = String.Join(' ', role.Users.Select(id => guild.GetUser(id).Mention));
                }
                builder.AddField($"{role.Definition.Emoji} {role.Definition.Name}", value, inline: true);

                componentBuilder.WithButton(new ButtonBuilder()
                    .WithEmote(new Emoji(role.Definition.Emoji))
                    .WithStyle(ButtonStyle.Secondary)
                    .WithCustomId($"{ChooseRoleAction.Name}_{(int)entry.Id}_{(int)role.Definition.RoleType}"));
            }

            componentBuilder.WithButton(emote: new Emoji("✅"), customId: "pvm-event-complete", style: ButtonStyle.Success);

            embed = builder.Build();
            component = componentBuilder.Build();
        }

        public static bool CanAddUserToRole(ulong id, IEnumerable<RuntimeRole> roles, out string[] conflictingRoles)
        {
            // TODO
            conflictingRoles = new string[0];
            return true;
        }

        public static RuntimeRole[] GetRuntimeRoles(Embed? embed = null)
        {
            var roles = new RuntimeRole[HostPvmCommand.Roles.Length];
            for (int i = 0; i < HostPvmCommand.Roles.Length; ++i)
            {
                List<ulong>? users = null;
                if (embed != null)
                {
                    var field = embed.Fields.ElementAt(i);
                    if (field.Value != "Empty")
                    {
                        users = field.Value.Split(' ').Select(mention => Utility.GetUserIdFromMention(mention)).ToList();
                    }
                }

                roles[i] = new RuntimeRole(HostPvmCommand.Roles[i], users);
            }
            return roles;
        }
    }
}
