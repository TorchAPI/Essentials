using System;

namespace Essentials
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ConditionAttribute : Attribute
    {
        public string Command;
        public string InvertCommand;
        public string HelpText;

        public ConditionAttribute(string command, string invertCommand = null, string helpText = null)
        {
            Command = command;
            InvertCommand = invertCommand;
            HelpText = helpText;
        }
    }
}