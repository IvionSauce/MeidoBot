using System;
using System.Collections.Generic;


namespace MeidoCommon.Parsing
{
    public class ArgEnumerator : IDisposable
    {
        public string CurrentArg;

        readonly IEnumerator<string> argNumerator;


        public ArgEnumerator(IEnumerable<string> argv)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));

            argNumerator = argv.GetEnumerator();
            CurrentArg = string.Empty;
        }


        public bool NextArg()
        {
            bool retval = false;

            while (!retval)
            {
                bool nextSucces = argNumerator.MoveNext();
                if (nextSucces)
                {
                    if (!string.IsNullOrWhiteSpace(argNumerator.Current))
                    {
                        CurrentArg = argNumerator.Current;
                        retval = true;
                    }
                }
                else
                {
                    CurrentArg = string.Empty;
                    break;
                }
            }

            return retval;
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

            CurrentArg = string.Empty;
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