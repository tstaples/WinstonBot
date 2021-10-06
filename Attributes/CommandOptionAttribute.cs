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
        public bool Required {  get; set; }

        public CommandOptionAttribute()
        {
            Name = null;
            Required = true;
        }

        public CommandOptionAttribute(string name, bool required = true)
        {
            Name = name;
            Required = required;
        }
    }
}
