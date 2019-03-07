using System;
using System.Collections.ObjectModel;


namespace MeidoCommon
{
    static class Tools
    {
        public static bool ContainsWhitespace(string s)
        {
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c))
                    return true;
            }

            return false;
        }


        public static ReadOnlyCollection<CommandHelp> ProcessCommands(
            CommandHelp[] collection,
            string argName)
        {
            if (collection == null)
                throw new ArgumentNullException(argName);
            if (collection.Length == 0)
                throw new ArgumentException("Cannot be an empty collection.", argName);

            var ourCopy = new CommandHelp[collection.Length];
            for (int i = 0; i < collection.Length; i++)
            {
                var el = collection[i];
                if (el != null)
                    ourCopy[i] = el;
                else
                    throw new ArgumentException("Cannot contain null.", argName);
            }

            return new ReadOnlyCollection<CommandHelp>(ourCopy);
        }
    }
}