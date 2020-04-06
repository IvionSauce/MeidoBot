using System;
using System.Collections.ObjectModel;
using MeidoCommon.ExtensionMethods;
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

                    // Copy such that we drop the trigger, it has its own propery.
                    if (argv.Length > 1)
                        arguments = argv.Subarray(1);

                    _arguments = new ReadOnlyCollection<string>(arguments ?? Array.Empty<string>());
                }
                return _arguments;
            }
        }


        public TriggerMsg(IrcMsg msg) : base(msg)
        {}
    }
}