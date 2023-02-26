using System;
using System.Reflection;
using Sandbox.Game.Entities;
using Torch.Commands;

namespace Essentials.Conditions
{
    public class Condition
    {
        public string Command;
        public string InvertCommand;
        public string HelpText;
        private MethodInfo _method;
        public readonly ParameterInfo Parameter;

        public Condition(MethodInfo evalMethod, ConditionAttribute attribute)
        {
            Command = attribute.Command;
            InvertCommand = attribute.InvertCommand;
            HelpText = attribute.HelpText;
            _method = evalMethod;
            if (_method.ReturnType != typeof(bool))
                throw new TypeLoadException("Condition does not return a bool!");
            var p = _method.GetParameters();
            if (p.Length < 1 || p[0].ParameterType != typeof(MyCubeGrid))
                throw new TypeLoadException("Condition does not accept MyCubeGrid as first parameter");
            if (p.Length > 2)
                throw new TypeLoadException("Condition can only have two parameters");
            if (p.Length == 1)
                Parameter = null;
            else
                Parameter = p[1];
        }

        public bool? Evaluate(MyCubeGrid grid, string arg, bool invert, CommandContext context)
        {
            bool result;
            if (!string.IsNullOrEmpty(arg) && Parameter == null)
            {
                context.Respond($"Condition does not accept an argument. Cannot continue!");
                return null;
            }
            if (string.IsNullOrEmpty(arg) && Parameter != null && !Parameter.HasDefaultValue)
            {
                context.Respond($"Condition requires an argument! {Parameter.ParameterType.Name}: {Parameter.Name} Not supplied, cannot continue!");
                return null;
            }
            if (Parameter != null && !string.IsNullOrEmpty(arg))
            {
                if (!arg.TryConvert(Parameter.ParameterType, out object val))
                {
                    context.Respond($"Could not parse argument!");
                    return null;
                }

                result = (bool)_method.Invoke(null, new[] { grid, val });
            }
            else
            {
                result = (bool)_method.Invoke(null, new object[] { grid });
            }

            return result != invert;
        }
    }
}