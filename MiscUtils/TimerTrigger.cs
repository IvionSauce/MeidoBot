using System;
using System.Collections.Generic;
using MeidoCommon;
using MeidoCommon.Parsing;


class TimerTrigger
{
    readonly IrcTimers ircTimers = new IrcTimers();


    public void Timer(ITriggerMsg e)
    {
        var timers = ircTimers.GetTimers(e.Nick);
        var command =
            e.GetArg(out List<string> rest)
            .ToLowerInvariant();

        // timer
        if (!command.HasValue())
        {
            EmitDescriptions(timers.Descriptions(), e);
            return;
        }

        // timer <delta> [index]
        // Delta is +N or -N
        char deltaSign = command[0];
        if (deltaSign == '+' || deltaSign == '-')
            TimerChange(
                ParseIdx(rest.GetArg(), 0),
                ParseTs(command),
                e, timers
            );

        // timer stop [index]
        else if (command == "stop")
            TimerStop(
                ParseIdx(rest.GetArg(), -1),
                e, timers
            );
        
        // timer change <index> <delta>
        else if (command == "change")
        {
            var args = rest.GetArgs(2);
            if (ParseArgs.Success(args))
                TimerChange(
                    ParseIdx(args[0], -1),
                    ParseTs(args[1]),
                    e, timers
                );
        }

        // timer <duration> [message]
        else
            TimerStart(
                ParseTs(command),
                rest.ToJoined(JoinedOptions.TrimExterior),
                e, timers
            );
    }


    // timer <duration> [message]
    static void TimerStart(TimeSpan duration, string message, ITriggerMsg e, Timers timers)
    {
        // Return if invalid or negative duration.
        if (duration <= TimeSpan.Zero)
            return;

        int tmrNo = timers.Enqueue(duration, e, message);
        if (tmrNo >= 0)
            e.Reply("Your timer has started. [{0}] {1}", tmrNo, duration.Str());
        else
            e.Reply("Max timer count reached. Please wait for some timers to finish or stop them manually.");
    }


    static void TimerChange(int index, TimeSpan delta, ITriggerMsg e, Timers timers)
    {
        // Return if invalid or 0 delta.
        if (delta == TimeSpan.Zero)
            return;

        if (timers.Change(index, delta, out TimerDescription desc))
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
    static void TimerStop(int index, ITriggerMsg e, Timers timers)
    {
        // Stop all timers.
        if (index < 0)
        {
            timers.StopAll();
            e.Reply("Stopped all your timers.");
        }
        // Stop one timer.
        else
        {
            if (timers.Stop(index, out TimerDescription desc))
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


    public static int ParseIdx(string s, int defaultValue)
    {
        if (int.TryParse(s, out int val))
            return val;
        else
            return defaultValue;
    }

    static TimeSpan ParseTs(string s)
    {
        TimeSpan ts = TimeSpan.Zero;

        // Interpret a bare number as minutes.
        if (double.TryParse(s, out double minutes))
            ts = TimeSpan.FromMinutes(minutes);
        // Otherwise assume it's a short time string.
        // Ex: 1h45m30s
        else
            ts = Parse.ShortTimeString(s);

        return ts;
    }
}