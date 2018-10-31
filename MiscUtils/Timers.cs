using System;
using System.Threading;
using System.Collections.Generic;
using MeidoCommon;


class IrcTimers
{
    readonly Dictionary<string, Timers> nickToTimers =
        new Dictionary<string, Timers>(StringComparer.OrdinalIgnoreCase);


    public Timers GetTimers(string nick)
    {
        const int MaxTimersCount = 5;

        Timers timers;
        if (!nickToTimers.TryGetValue(nick, out timers))
        {
            timers = new Timers(MaxTimersCount);
            nickToTimers[nick] = timers;
        }
        return timers;
    }

    public static TimeSpan Parse(string s)
    {
        double minutes;
        if (double.TryParse(s, out minutes))
            return TimeSpan.FromMinutes(minutes);
        else
            return TimeSpan.Zero;
    }

    public static int Parse(string s, int defaultValue)
    {
        int val;
        if (int.TryParse(s, out val))
            return val;
        else
            return defaultValue;
    }
}


class Timers
{
    readonly object _locker = new object();
    readonly SingleTimer[] timers;


    public Timers(int capacity)
    {
        timers = new SingleTimer[capacity];
    }


    public int Enqueue(TimeSpan duration, ITriggerMsg msg, string message)
    {
        lock (_locker)
        {
            for (int i = 0; i < timers.Length ; i++)
            {
                // Null indicates a vacancy.
                if (timers[i] == null)
                {
                    timers[i] = new SingleTimer(i, Remove, msg);
                    timers[i].Start(duration);
                    timers[i].Message = message;
                    return i;
                }
            }
        }
        return -1;
    }


    public TimerDescription[] Descriptions()
    {
        var desc = new List<TimerDescription>();
        lock (_locker)
        {
            foreach (SingleTimer tmr in timers)
            {
                if (tmr != null)
                    desc.Add( new TimerDescription(tmr) );
            }
        }
        return desc.ToArray();
    }


    public bool Change(int index, TimeSpan difference, out TimerDescription desc)
    {
        if (index >= 0 && index < timers.Length)
        {
            lock (_locker)
            {
                if (timers[index] != null)
                {
                    timers[index].Change(difference);
                    desc = new TimerDescription(timers[index]);
                    return true;
                }
            }
        }
        desc = null;
        return false;
    }


    public bool Stop(int index, out TimerDescription desc)
    {
        if (index >= 0 && index < timers.Length)
        {
            lock (_locker)
            {
                if (timers[index] != null)
                {
                    desc = new TimerDescription(timers[index]);

                    timers[index].Dispose();
                    timers[index] = null;
                    return true;
                }
            }
        }
        desc = null;
        return false;
    }

    public void StopAll()
    {
        lock (_locker)
        {
            for (int i = 0; i < timers.Length; i++)
            {
                if (timers[i] != null)
                {
                    timers[i].Dispose();
                    timers[i] = null;
                }
            }
        }
    }

    void Remove(int index)
    {
        lock (_locker)
        {
            timers[index].Dispose();
            timers[index] = null;
        }
    }
}


class TimerDescription
{
    public readonly int Index;
    public readonly string Message;
    public readonly TimeSpan Duration;
    public readonly TimeSpan Elapsed;
    public readonly TimeSpan Remaining;


    public TimerDescription()
    {
        Index = -1;
    }

    public TimerDescription(SingleTimer tmr)
    {
        Index = tmr.Index;
        Message = tmr.Message;
        Duration = tmr.Duration;

        var now = DateTimeOffset.Now;
        Elapsed = now - tmr.StartTime;
        Remaining = now - tmr.StopTime;
    }
}


class SingleTimer : IDisposable
{
    public readonly int Index;
    Action<int> Remove;

    public string Message { get; set; }

    public TimeSpan Duration { get; private set; }

    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset StopTime { get; private set; }

    readonly Timer tmr;
    readonly ITriggerMsg msg;


    public SingleTimer(int index, Action<int> remove, ITriggerMsg msg)
    {
        Index = index;
        Remove = remove;
        tmr = new Timer(Finish);
        this.msg = msg;
    }


    public void Start(TimeSpan duration)
    {
        Duration = duration;
        tmr.Change(duration, TimeSpan.Zero);
        StartTime = DateTimeOffset.Now;
        StopTime = StartTime + duration;
    }

    public void Change(TimeSpan difference)
    {
        Duration += difference;
        StopTime = StartTime + Duration;

        TimeSpan newInterval;
        if (Duration > TimeSpan.Zero)
            newInterval = StopTime - DateTimeOffset.Now;
        else
            newInterval = TimeSpan.Zero;

        tmr.Change(newInterval, TimeSpan.Zero);
    }

    void Finish(object state)
    {
        if (string.IsNullOrEmpty(Message))
            msg.Reply("Timer {0} has finished! :: {1}", Index, Duration);
        else
            msg.Reply("{0} :: {1}", Message, Duration);

        Remove(Index);
    }

    public void Dispose()
    {
        if (tmr != null)
            tmr.Dispose();
    }
}