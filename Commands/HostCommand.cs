using Discord;
using Discord.Commands;
using WinstonBot.Services;
using WinstonBot.MessageHandlers;
using Microsoft.Extensions.DependencyInjection;

namespace WinstonBot.Commands
{
	[Group("host")]
	public class HostModule : ModuleBase<CommandContext>
    {
		//public static string SignupEmoteName = "winstonface";
		//public static string CompleteEmoji = "\u2705";

		private MessageDatabase _messageDB;

		public HostModule(MessageDatabase messageDB)
        {
			_messageDB = messageDB;
        }

		[Group("aod")]
		public class AoD : ModuleBase<CommandContext>
		{
			public MessageDatabase MessageDB { get; set; }
			public EmoteDatabase EmoteDatabase { get; set; }

			[Command("queued")]
			public async Task Queued()
			{
				Console.WriteLine("host queued aod");

				var signUpEmote = EmoteDatabase.Get(Context.Client, EmoteDatabase.AoDEmote);
				if (signUpEmote == null)
				{
					await Context.Channel.SendMessageAsync("Failed to find signup reaction emote");
					return;
				}

				var completeEmote = EmoteDatabase.Get(Context.Client, EmoteDatabase.CompleteEmoji);

				var message = await Context.Channel.SendMessageAsync($"React with {signUpEmote.ToString()} to sign up for AoD");

				//var handler = new AoDMessageHandlers.QueueCompleted(Context, new UserReader(Context.Client));
				var testNames = Context.ServiceProvider.GetRequiredService<ConfigService>().Configuration.DebugTestNames;
				var handlerContext = new MessageHandlerContext(Context.GuildId, Context.ServiceProvider, new MockUserReader(testNames));
				var handler = new AoDMessageHandlers.QueueCompleted(handlerContext);
				MessageDB.AddMessage(Context.Guild.Id, message.Id, handler);

				await message.AddReactionsAsync(new IEmote[] { signUpEmote, completeEmote });
			}
		}

		[Command("aod")]
		public async Task HostAod()
		{
			Console.WriteLine("host default aod");

			var builder = new ComponentBuilder()
				.WithButton("Sign Up", "aod-signup");

			await ReplyAsync("Click to signup for AoD.", component: builder.Build());
		}
	}
}
