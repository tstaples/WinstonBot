using System;
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
        public Type ParentCommand { get; set; }

        public SubCommandAttribute()
        {
            Name = null;
            ParentCommand = null;
        }

        public SubCommandAttribute(string name, Type parentCommand)
        {
            Name = name;
            ParentCommand = parentCommand;
        }
    }
}
