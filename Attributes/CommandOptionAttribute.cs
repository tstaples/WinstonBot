using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CommandOptionAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Required {  get; set; }

        public CommandOptionAttribute()
        {
            Name = null;
            Description = "PLEASE FILL ME OUT";
            Required = true;
        }

        public CommandOptionAttribute(string name, string description, bool required = true)
        {
            Name = name;
            Description = description;
            Required = required;
        }
    }
}
