using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    public static class PluginExtensions
    {
        public static string Name(this IMeidoHook plugin)
        {
            string name = plugin.Name.Trim();
            switch (name)
            {
                case "":
                case null:
                name = "Unknown";
                break;
                // Reserved names.
                case "Main":
                case "Meido":
                case "Triggers":
                case "Auth":
                name = "_" + name;
                break;
            }

            return name;
        }

        public static Dictionary<string, string> Help(this IMeidoHook plugin)
        {
            if (plugin.Help != null)
                return plugin.Help;

            return new Dictionary<string, string>();
        }


        public static IEnumerable<T> NoNull<T>(this IEnumerable<T> seq) where T : class
        {
            if (seq != null)
            {
                foreach (T item in seq)
                {
                    if (item != null)
                        yield return item;
                }
            }
        }
    }
}