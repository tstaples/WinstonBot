# WinstonBot
Vaught Discord bot

## Setup
1. In the root project folder, add a folder named Config.
2. In here add a file called appsettings.Development.json that has a key called "token" with the value of your development token. Release token is read appsettings.Production.json.
3. Also add your google credentials file and name it google_credentials.json
4. In VS, set these files to auto-copy to the output directory.
5. You can run production or release based on the DOTNET_ENVIRONMENT env var which can be set in the VS launch options.

Example launch settings:

```
{
  "profiles": {
    "WinstonBot": {
      "commandName": "Project",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

If you're only testing a particular command you can add it to the allow list in your appsettings.Development.json so only it gets registered. This is suggested to avoid polluting the slash command table with test versions of all the commands.
```
  "allowed_commands": [
    "host-pvm-signup"
  ]
  ```

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

## Actions
Actions are used to handle component interactions (eg. a button press).

Currently actions can be defined generically and a command must list in their CommandAttribute which actions they use.

You can define an action similar to how you define a command:
1. Implement the IAction interface.
2. Add the `[Action]` attribute.

When building a component you can specify which action should handle it by setting the first part of the custom_id to the action's name.
You can also pass additional parameters through the custom id to your action by separating them with underscores.
These parameters must match properties on your action that have the `[ActionParam]` attribute. The order the arguments are passed in the custom id must match the order the properties in your action are defined.

Example:

```cs
[Action]
internal class MyButtonAction : IAction
{
  public static string ActionName = "MyButton";
  
  [ActionParam]
  public long Value { get; set; }
  
  public async Task HandleAction(ActionContext actionContext)
  {
    await actionContext.RespondAsync($"You Pressed button {Value}");
  }
}

...
[Command("number-picker", "Pick a number, any number", actions: new Type[] { typeof(MyButtonAction) })]
public class NumberPickerCommand : CommandBase
{
  public async override Task HandleCommand(CommandContext context)
  {  
    var componentBuilder = new ComponentBuilder();
    for (int i = 0; i < 5; ++i)
    {
      componentBuilder
        .WithButton(new ButtonBuilder()
          .WithLabel($"Button {i}")
          // the value of i will be injected into the Value property of MyButtonAction.
          .WithCustomId($"{MyButtonAction.ActionName}_{i}"));
    }
    
    await context.RespondAsync("Pick a number", components:buttonComponent.Build());
  }
}
```
