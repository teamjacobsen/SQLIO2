using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SQLIO2
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            switch (args.FirstOrDefault())
            {
                case "client":
                    return await ParseCommand<ClientCommand>(args.Skip(1)).HandleAsync();
                case "proxy":
                    return await ParseCommand<ProxyCommand>(args.Skip(1)).HandleAsync();
                default:
                    return 1;
            }
        }

        private static TCommand ParseCommand<TCommand>(IEnumerable<string> args) where TCommand : new()
        {
            var cmd = new TCommand();
            var properties = cmd.GetType().GetProperties();

            var options = new Dictionary<string, PropertyInfo>();
            var arguments = new SortedList<int, PropertyInfo>();

            foreach (var property in properties)
            {
                var optionAttribute = property.GetCustomAttribute<OptionAttribute>();

                if (optionAttribute != null)
                {
                    var template = optionAttribute.Template;

                    var offset = 0;
                    var optionLength = template.IndexOf('|');

                    while (optionLength >= 0)
                    {
                        options.Add(template.AsSpan(offset, optionLength).ToString(), property);

                        offset = offset + optionLength + 1;
                        optionLength = template.IndexOf('|', offset);
                    }

                    options.Add(template.AsSpan(offset, template.Length - offset).ToString(), property);
                }

                var argumentAttribute = property.GetCustomAttribute<ArgumentAttribute>();

                if (argumentAttribute != null)
                {
                    arguments.Add(argumentAttribute.Order, property);
                }
            }

            PropertyInfo currentOptionProperty = null;
            var nextArgumentIndex = 0;

            foreach (var arg in args)
            {
                if (currentOptionProperty != null)
                {
                    var converted = ConvertArgument(arg, currentOptionProperty.PropertyType);
                    currentOptionProperty.SetValue(cmd, converted);

                    currentOptionProperty = null;
                }
                else if (options.TryGetValue(arg, out var property))
                {
                    currentOptionProperty = property;
                }
                else if (nextArgumentIndex < arguments.Count)
                {
                    property = arguments[nextArgumentIndex];

                    var converted = ConvertArgument(arg, property.PropertyType);
                    property.SetValue(cmd, converted);

                    nextArgumentIndex++;
                }
            }

            static object ConvertArgument(string arg, Type propertyType) => Convert.ChangeType(arg, Nullable.GetUnderlyingType(propertyType) ?? propertyType);

            return cmd;
        }
    }
}
