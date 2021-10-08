using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinstonBot
{
    internal class Utility
    {
        public static GuildEmote TryGetEmote(DiscordSocketClient client, string name)
        {
            return client.Guilds.SelectMany(x => x.Emotes)
                .FirstOrDefault(x => x.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) != -1);
        }

        public static ulong GetUserIdFromMention(string mention)
        {
            var resultString = Regex.Match(mention, @"\d+").Value;
            ulong value = 0;
            if (ulong.TryParse(resultString, out value))
            {
                return value;
            }
            else
            {
                Console.WriteLine($"Failed to parse user id from string {mention}");
                return 0;
            }
        }
        public static List<string> ConvertUserIdListToMentions(SocketGuild guild, IEnumerable<ulong> ids)
        {
            return ids.Select(id => guild.GetUser(id)?.Mention).ToList();
        }

        public static EmbedBuilder CreateBuilderForEmbed(IEmbed source)
        {
            // TODO: we're currently missing a couple fields - add them.
            var builder = new EmbedBuilder()
                .WithTitle(source.Title)
                .WithDescription(source.Description)
                .WithUrl(source.Url);
            if (source.Timestamp != null) builder.WithTimestamp(source.Timestamp.Value);
            if (source.Color != null) builder.WithColor(source.Color.Value);
            if (source.Image != null) builder.WithImageUrl(source.Image.Value.Url);
            if (source.Thumbnail != null) builder.WithThumbnailUrl(source.Thumbnail.Value.Url);
            var fields = source.Fields.Select(field =>
            {
                return new EmbedFieldBuilder()
                    .WithName(field.Name)
                    .WithValue(field.Value)
                    .WithIsInline(field.Inline);

            });
            builder.WithFields(fields.ToArray());

            if (source.Author != null)
            {
                builder.WithAuthor(source.Author.Value.Name, source.Author.Value.IconUrl, source.Author.Value.Url);
            }
            if (source.Footer != null)
            {
                builder.WithFooter(source.Footer.Value.Text, source.Footer.Value.IconUrl);
            }
            return builder;
        }

        public static string JoinRoleNames(SocketGuild guild, IEnumerable<ulong> roles)
        {
            return String.Join(", ", roles.Select(roleId => guild.GetRole(roleId)));
        }

        public static string JoinRoleMentions(SocketGuild guild, IEnumerable<ulong> roles)
        {
            return String.Join('\n', roles.Select(roleId => guild.GetRole(roleId).Mention));
        }

        public static bool DoesUserHaveAnyRequiredRole(SocketGuildUser user, IEnumerable<ulong> roles)
        {
            return !roles.Any() || user.Roles
                .Where(role => roles.Contains(role.Id))
                .Any();
        }

        public static bool AddUnique<T>(List<T> list, T value)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
                return true;
            }
            return false;
        }

        public static TValue GetOrAdd<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key) where TValue : new()
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, new TValue());
            }
            return dict[key];
        }

        public static MethodInfo? GetInheritedStaticMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public);
        }
    }
}
