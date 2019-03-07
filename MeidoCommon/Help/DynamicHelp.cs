using System;
using System.Collections.Generic;


namespace MeidoCommon
{
    class DynamicHelp
    {
        public readonly IEnumerable<string> Documentation;

        static readonly char[] delimiters = {'\n', '\r'};


        public DynamicHelp()
        {
            Documentation = new string[0];
        }

        public DynamicHelp(string documentation)
        {
            if (documentation == null)
                throw new ArgumentNullException(nameof(documentation));
            
            // IEnumerable<string> is actually string[].
            // No deferred execution happening here.
            Documentation = Split(documentation);
        }

        public DynamicHelp(Func<string> documentation)
        {
            if (documentation == null)
                throw new ArgumentNullException(nameof(documentation));
            
            // Generator is wrapped such as to produce an iterator.
            // Execution of Func<string> is deferred until `Documentation` is iterated over.
            Documentation = WrapGenerator(documentation);
        }

        public DynamicHelp(IEnumerable<string> documentation)
        {
            if (documentation == null)
                throw new ArgumentNullException(nameof(documentation));
            
            // A straight up IEnumerable<string>.
            // Thus the caller can decide if they want a stateful iterator or a normal collection.
            Documentation = documentation;
        }

        // There used to be a constructor for Func<IEnumerable<string>> here. It's gone now.


        static string[] Split(string s)
        {
            if (s != null)
                return s.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            else
                return new string[0];
        }

        static IEnumerable<string> WrapGenerator(Func<string> docGenerator)
        {
            foreach ( string s in Split(docGenerator()) )
                yield return s;
        }
    }
}