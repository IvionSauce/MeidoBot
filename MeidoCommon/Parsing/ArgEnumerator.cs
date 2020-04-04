using System;
using System.Linq;
using System.Collections.Generic;


namespace MeidoCommon.Parsing
{
    public class ArgEnumerator : IDisposable
    {
        public bool ToLower { get; set; }

        string _current;
        public string Current
        {
            get { return _current; }
            private set
            {
                if (ToLower)
                    _current = value.ToLowerInvariant();
                else
                    _current = value;
            }
        }

        readonly IEnumerator<string> argNumerator;


        public ArgEnumerator(IIrcMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            argNumerator =
                msg.MessageArray
                .Skip(1)
                .GetEnumerator();

            Current = string.Empty;
        }

        public ArgEnumerator(IEnumerable<string> argv)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));

            argNumerator = argv.GetEnumerator();
            Current = string.Empty;
        }


        public bool MoveNext()
        {
            bool gotArg = false;

            while (!gotArg)
            {
                if (argNumerator.MoveNext())
                {
                    gotArg = ParseArgs.TryGetArg(argNumerator.Current, out string arg);
                    Current = arg;
                }
                else
                {
                    Current = string.Empty;
                    break;
                }
            }

            return gotArg;
        }

        public string Next()
        {
            // Nothing can slow me down; two actions to get the next arg? Tedious!
            MoveNext();
            return Current;
        }


        public List<string> GetRemaining()
        {
            return GetRemaining(false);
        }

        public List<string> GetRemaining(bool removeEmptyOrWhitespace)
        {
            var remain = new List<string>();
            while (argNumerator.MoveNext())
            {
                if ( !(string.IsNullOrWhiteSpace(argNumerator.Current) && removeEmptyOrWhitespace) )
                    remain.Add(argNumerator.Current);
            }

            Current = string.Empty;
            return remain;
        }


        public void Reset()
        {
            argNumerator.Reset();
        }

        public void Dispose()
        {
            argNumerator.Dispose();
        }
    }
}