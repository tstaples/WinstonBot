﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SubCommandAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Type ParentCommand { get; set; }
        public bool HasDynamicSubCommands { get; set; }
        public Type[]? Actions { get; set; }

        public SubCommandAttribute()
        {
            Name = null;
            Description = "PLEASE FILL ME OUT";
            ParentCommand = null;
            HasDynamicSubCommands = false;
            Actions = null;
        }

        public SubCommandAttribute(string name, string description, Type parentCommand, Type[]? actions = null, bool dynamicSubcommands = false)
        {
            Name = name;
            Description = description;
            ParentCommand = parentCommand;
            Actions = actions;
            HasDynamicSubCommands = dynamicSubcommands;
        }
    }
}
