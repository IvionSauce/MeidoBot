using System;
using MeidoCommon;
using MeidoCommon.Parsing;


class TimerTrigger
{
    readonly IrcTimers ircTimers = new IrcTimers();


    public void Timer(ITriggerMsg e)
    {
        var timers = ircTimers.GetTimers(e.Nick);

        // timer
        if (e.MessageArray.Length == 1)
        {
            EmitDescriptions(timers.Descriptions(), e);
            return;
        }

        // timer <delta> [index]
        // Delta is +N or -N
        if (e.MessageArray[1].Length > 1)
        {
            char deltaSign = e.MessageArray[1][0];
            if (deltaSign == '+' || deltaSign == '-')
            {
                TimerChangeShorthand(e, timers);
                return;
            }
        }

        // timer stop [index]
        if (e.MessageArray[1] == "stop")
            TimerStop(e, timers);
        
        // timer change <index> <delta>
        else if (e.MessageArray.Length == 4 && e.MessageArray[1] == "change")
            TimerChange(e, timers);

        // timer <duration> [message]
        else
            TimerStart(e, timers);
    }


    // timer <duration> [message]
    static void TimerStart(ITriggerMsg e, Timers timers)
    {
        // Return if invalid or negative duration.
        var duration = ParseTs(e.MessageArray[1]);
        if (duration <= TimeSpan.Zero)
            return;

        // Grab message if one is given.
        string message = null;
        if (e.MessageArray.Length > 2)
            message = string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2);

        int tmrNo = timers.Enqueue(duration, e, message);
        if (tmrNo >= 0)
            e.Reply("Your timer has started. [{0}] {1}", tmrNo, duration.Str());
        else
            e.Reply("Max timer count reached. Please wait for some timers to finish or stop them manually.");
    }


    // timer change <index> <delta>
    static void TimerChange(ITriggerMsg e, Timers timers)
    {
        var delta = ParseTs(e.MessageArray[3]);
        int index = IrcTimers.Parse(e.MessageArray[2], -1);

        TimerChange(index, delta, e, timers);
    }

    // timer <delta> [index]
    // Delta is +N or -N
    static void TimerChangeShorthand(ITriggerMsg e, Timers timers)
    {
        var delta = ParseTs(e.MessageArray[1]);
        int index;
        if (e.MessageArray.Length >= 3)
            index = IrcTimers.Parse(e.MessageArray[2], -1);
        else
            index = 0;

        TimerChange(index, delta, e, timers);
    }

    static void TimerChange(int index, TimeSpan delta, ITriggerMsg e, Timers timers)
    {
        // Return if invalid or 0 delta.
        if (delta == TimeSpan.Zero)
            return;

        TimerDescription desc;
        if (timers.Change(index, delta, out desc))
        {
            if (string.IsNullOrEmpty(desc.Message))
                e.Reply("Changed timer {0} to {1} ({2}).",
                        index, desc.Duration.Str(), desc.Remaining.Str());
            else
                e.Reply("Changed \"{0}\" to {1} ({2}).",
                        desc.Message, desc.Duration.Str(), desc.Remaining.Str());
        }
        else
            e.Reply("No such timer.");
    }


    // timer stop [index]
    static void TimerStop(ITriggerMsg e, Timers timers)
    {
        // Stop all timers.
        if (e.MessageArray.Length == 2)
        {
            timers.StopAll();
            e.Reply("Stopped all your timers.");
        }
        // Stop one timer.
        else
        {
            int index = IrcTimers.Parse(e.MessageArray[2], -1);
            TimerDescription desc;
            if (timers.Stop(index, out desc))
            {
                if (string.IsNullOrEmpty(desc.Message))
                    e.Reply("Stopped timer {0} :: {1}/{2}",
                            index, desc.Elapsed.Str(), desc.Duration.Str());
                else
                    e.Reply("Stopped \"{0}\" :: {1}/{2}",
                            desc.Message, desc.Elapsed.Str(), desc.Duration.Str());
            }
            else
                e.Reply("No such timer.");
        }
    }


    static TimeSpan ParseTs(string s)
    {
        TimeSpan ts = TimeSpan.Zero;

        double minutes;
        // Interpret a bare number as minutes.
        if (double.TryParse(s, out minutes))
            ts = TimeSpan.FromMinutes(minutes);
        // Otherwise assume it's a short time string.
        // Ex: 1h45m30s
        else
            ts = Parse.ShortTimeString(s);

        return ts;
    }


    static void EmitDescriptions(TimerDescription[] descs, ITriggerMsg msg)
    {
        msg.SendNotice("Your currently running timers:");
        foreach (var desc in descs)
            EmitDescription(desc, msg);
        msg.SendNotice(" -----");
    }

    static void EmitDescription(TimerDescription desc, ITriggerMsg msg)
    {
        msg.SendNotice("[{0}] {1} :: {2} ({3})",
                       desc.Index, desc.Message, desc.Duration.Str(), desc.Remaining.Str());
    }
}