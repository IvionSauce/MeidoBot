using System;
using System.Collections.ObjectModel;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    class TriggerMsg : IrcMsg, ITriggerMsg
    {
        ReadOnlyCollection<string> _arguments;
        public ReadOnlyCollection<string> Arguments
        {
            get
            {
                // Lazily initialize `Arguments`.
                if (_arguments == null)
                {
                    string[] arguments = null;
                    // Includes the trigger at index 0.
                    var argv = Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                    if (argv.Length > 1)
                    {
                        // Copy such that we drop the trigger, it has its own propery.
                        arguments = new string[argv.Length - 1];
                        Array.Copy(argv, 1, arguments, 0, arguments.Length);
                    }

                    _arguments = new ReadOnlyCollection<string>(arguments ?? Array.Empty<string>());
                }
                return _arguments;
            }
        }


        public TriggerMsg(IrcMsg msg) : base(msg)
        {}
    }
}