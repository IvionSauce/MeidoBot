using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

// IMeidoHook wants you to implement `Name`, `Version`, `Help` and the `Stop` method.
[Export(typeof(IMeidoHook))]
public class MyClass : IMeidoHook
{
    public string Name
    {
        get { return "MyClass"; }
    }
    public string Version
    {
        get { return "0.10"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"example", "example [args] - does something."}
            };
        }
    }

    public void Stop()
    {}

    [ImportingConstructor]
    public MyClass(IIrcComm irc, IMeidoComm meido)
    {
        meido.RegisterTrigger("example", ExampleTrigger);
    }

    public void ExampleTrigger(IIrcMessage ircMessage)
    {
        // Do something, like:
        ircMessage.Reply("Example trigger triggered.");
    }
}