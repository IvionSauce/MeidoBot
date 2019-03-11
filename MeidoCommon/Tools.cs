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
            string argName,
            IHelpNode parentNode)
        {
            if (collection == null)
                throw new ArgumentNullException(argName);
            if (collection.Length == 0)
                throw new ArgumentException("Cannot be an empty collection.", argName);

            /* Create our own copy of the array, so it cannot be changed by the caller.
             * But also clone the elements, not just copy the reference. This to protect
             * a caller that reuses the same branch with different parents from inadvertently
             * having the parent of the branch be the last parent used.
            */
            var ourCopy = new CommandHelp[collection.Length];
            for (int i = 0; i < collection.Length; i++)
            {
                var el = collection[i];
                if (el != null)
                    ourCopy[i] = el.ReparentClone(parentNode);
                else
                    throw new ArgumentException("Cannot contain null.", argName);
            }

            return new ReadOnlyCollection<CommandHelp>(ourCopy);
        }
    }
}