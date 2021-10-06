using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class CommandAttribute : Attribute
    {
        public string Name { get; set; }

        public CommandAttribute()
        {
            Name = null;
        }

        public CommandAttribute(string name)
        {
            Name = name;
        }
    }
}
