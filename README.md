# WinstonBot
Vaught Discord bot

## Setup
1. In the root project folder, add a folder named Config.
2. In here add a file called test_token.txt that has your development token. Release token is read from token.txt.
3. Also add your google credentials file and name it google_credentials.json
4. In VS, set these files to auto-copy to the output directory.

## Adding a Command
1. Under the Commands folder add a new source file for your command.
2. Inherit from CommandBase
3. Add the `[Command]` Attribute. This will include your command name (lower case), description, default permissions (if it's admin only), and optional list of actions (more on those later).
4. Implement `Task HandleCommand(CommandContext context)` to do stuff in your command.

### Adding options to your command
Properties that have the `[CommandOption]` attribute are automatically picked up and added as options to your command.
These properties will contain the value of that command when HandleCommand is called.
The supported property types are:
* string
* long
* bool
* double
* SocketGuildUser
* SocketGuildChannel
* SocketRole

By default options are required. You can pass in required:false to the attribute to make it optional. If it's optional it's suggested to make the property nullable.

If your option has choices you can set the dataProvider argument to a class that has a function with the signature:
`public static void PopulateChoices(SlashCommandOptionBuilder builder)`
This function will be invoked to populate the choices of your option when commands are built.

### Sub Commands
You can add subcommands to your command by creating a class just like you did for your command, but use the `[SubCommand]` attribute instead.
In the constructor for this attribute set the parent type to the type of the parent command.

### Customizing the building of your command
Commands are auto-generated from metadata, but you can choose to build it yourself if you wish. You can do this by adding `static new SlashCommandBuilder BuildCommand(SlashCommandBuilder defaultBuider)` to your command/subcommand class.
The default builder that was generated is passed in, so you can just modify that if you wish, or start from scratch.

### Custom handling for subcommands
If you override `virtual bool WantsToHandleSubCommands` to return true and `virtual Task HandleSubCommand(CommandContext context, CommandInfo subCommandInfo, IEnumerable<CommandDataOption>? options)` in your command/subcommand class you can handle subcommands that don't have classes.
This is handy for dynamically generated sub-commands.

### Making your command schedulable
Add the `[Scheduleable]` attribute to your command and it will appear under the /schedule command.

### Making your command configurable
Add the `[Configurable]` attribute to your command it it will appear under the /configure command.
/Configure lets you set what roles can use that command.
