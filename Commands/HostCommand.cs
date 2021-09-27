using Discord;
using Discord.Commands;
using WinstonBot.Services;

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

				var message = await Context.Channel.SendMessageAsync($"React with {signUpEmote.ToString()} to sign up");

				MessageDB.AddMessage(message.Id, MessageDatabase.MessageType.AoD, MessageDatabase.GroupType.Queued);

				await message.AddReactionAsync(signUpEmote);
				await message.AddReactionAsync(completeEmote);
			}
		}

		[Command("aod")]
		public async Task HostAod()
		{
			Console.WriteLine("host default aod");

			//var signUpEmote = Utility.TryGetEmote(Context.Client, SignupEmoteName);
			//if (signUpEmote == null)
			//{
			//	await Context.Channel.SendMessageAsync("Failed to find signup reaction emote");
			//	return;
			//}

			//var completeEmote = Utility.TryGetEmote(Context.Client, CompleteEmoteName);
			//if (completeEmote == null)
			//{
			//	await Context.Channel.SendMessageAsync("Failed to find complete reaction emote");
			//	return;
			//}

			//var message = await Context.Channel.SendMessageAsync($"React with {signUpEmote.ToString()} to sign up");

			//Context.MessageDatabase.AddMessage(message.Id);

			//await message.AddReactionAsync(signUpEmote);
			//await message.AddReactionAsync(completeEmote);
		}
	}
}
