using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Services
{
    public class InteractionService
    {
        private class CommandEntry
        { 
            public string OwningCommand {  get; set; }
            public ulong MessageId {  get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class Storage
        {
            public List<CommandEntry> CommandEntries { get; set; } = new();
        }

        private Storage _storage = new();

        public void AddInteraction(string owningCommand, ulong messageId)
        {
            lock (_storage)
            {
                _storage.CommandEntries.Add(new CommandEntry()
                {
                    OwningCommand = owningCommand,
                    MessageId = messageId,
                    Timestamp = DateTime.UtcNow,
                });
            }
        }

        // Returns the name of the command that owns the interaction associated with the message id.
        public string? TryGetOwningCommand(ulong messageId)
        {
            lock (_storage)
            {
                CommandEntry? entry = _storage.CommandEntries.Find(entry => entry.MessageId == messageId);
                if (entry != null)
                {
                    return entry.OwningCommand;
                }
                return null;
            }
        }
    }
}
