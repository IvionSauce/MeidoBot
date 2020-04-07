using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Runtime.Serialization;
using System.Collections.Generic;
using IvionSoft;
using MeidoCommon.Parsing;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class TimeLeft : IMeidoHook, IPluginTriggers
{
    public string Name
    {
        get { return "TimeLeft"; }
    }
    public string Version
    {
        get { return "0.34"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    readonly IIrcComm irc;

    readonly Storage<TimeLeftUnit> storage;
    readonly Timer cleaner;
    readonly object _locker = new object();

    readonly string loc;
    const string dateFmt = "MMMM dd, yyyy";

    public void Stop()
    {
        cleaner.Dispose();
    }
    
    [ImportingConstructor]
    public TimeLeft(IIrcComm ircComm, IMeidoComm meido)
    {
        loc = Path.Combine(meido.DataDir, "_timeleft.xml");

        try
        {
            storage = Storage<TimeLeftUnit>.Deserialize(loc);
        }
        catch (FileNotFoundException)
        {
            storage = new Storage<TimeLeftUnit>();
        }

        cleaner = new Timer(Cleaner, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        irc = ircComm;

        Triggers = new Trigger[] {
            
            new Trigger("timeleft", HandleTrigger) {
                Help = new TriggerHelp(
                    "[name]",
                    "Check on the timeleft of `name`, name doesn't have to be exact. If no name is specified, will " +
                    "show all currently tracking names.",

                    new CommandHelp(
                        "set", "<name> <date>",
                        "Date must be in YYYY-MM-DD. Will replace entry if already exists."),
                    
                    new CommandHelp(
                        "del", "<name>",
                        "Removes exact name (case insensitive) from tracking.")
                )
            }
        };
    }
    
    public void HandleTrigger(ITriggerMsg e)
    {
        var rest = e.GetArg(out string command, toLower: true);

        // timeleft
        if (!command.HasValue())
        {
            ShowAll(e.ReturnTo);
            return;
        }

        // timeleft set Title 2104-01-01
        if (command == "set" || command == "add")
        {
            Set(e.Nick, rest);
        }

        // timeleft del Title
        else if (command == "del")
        {
            Del(e.Nick, rest.ToJoined(JoinedOptions.TrimRemove));
        }

        // timeleft Title
        else
            Show(e.ReturnTo, e.ArgString());
    }


    void Set(string nick, IEnumerable<string> args)
    {
        var name =
            args.GetEndArg(out string dateStr)
            .ToJoined(JoinedOptions.TrimRemove);

        if (TryGetDate(dateStr, out DateTime date))
        {
            var unit = new TimeLeftUnit(name, date);

            lock (_locker)
            {
                storage.Set(name, unit);
                storage.Serialize(loc);
            }
            irc.SendNotice(nick, "Set \"{0}\" :: {1}", name, date.ToString(dateFmt));
        }
    }

    bool TryGetDate(string arg, out DateTime date)
    {
        date = DateTime.MinValue;

        string[] dateArray = arg.Split('-');
        if (dateArray.Length == 3 &&
            int.TryParse(dateArray[0], out int year) &&
            int.TryParse(dateArray[1], out int month) &&
            int.TryParse(dateArray[2], out int day))
        {
            try
            {
                date = new DateTime(year, month, day);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {}
        }

        return false;
    }


    void Del(string nick, string name)
    {
        bool removed;
        lock (_locker)
        {
            removed = storage.Remove(name);
            if (removed)
                storage.Serialize(loc);
        }

        if (removed)
            irc.SendNotice(nick, "Deleted: {0}", name);
    }


    void Show(string target, string name)
    {
        TimeLeftUnit unit;
        lock (_locker)
            unit = storage.Get(name);
        if (unit != null)
            SendTime(target, unit.Name, unit.Date);

        // If no exact match, try to search for it.
        else
        {
            TimeLeftUnit[] sortedByTime;
            lock (_locker)
                sortedByTime = SortByDate( storage.Search(name) );

            foreach (var tlu in sortedByTime)
                SendTime(target, tlu.Name, tlu.Date);
        }
    }


    void ShowAll(string target)
    {
        TimeLeftUnit[] sortedByTime;
        lock (_locker)
            sortedByTime = SortByDate( storage.GetAll() );

        foreach (var unit in sortedByTime)
            SendTime(target, unit.Name, unit.Date);
    }


    static TimeLeftUnit[] SortByDate(IEnumerable<TimeLeftUnit> tlunits)
    {
        return
            tlunits.OrderBy(tlu => tlu.Date)
            .ToArray();
    }


    void SendTime(string target, string name, DateTime date)
    {
        TimeSpan timeLeft = date - DateTime.UtcNow;
        string message;
        if (timeLeft.Days >= 2)
        {
            message = string.Format("Days: {0} :: {1} [{2}]",
                                    timeLeft.Days, name, date.ToString(dateFmt));
        }
        else
        {
            message = string.Format("Hours: {0:0.#} :: {1} [{2}]",
                                    timeLeft.TotalHours, name, date.ToString(dateFmt));
        }
        
        irc.SendMessage(target, message);
    }


    void Cleaner(object data)
    {
        lock (_locker)
        {
            bool modified = false;

            foreach ( TimeLeftUnit unit in storage.GetAll() )
            {
                if (DateTime.UtcNow > unit.Date)
                {
                    TimeSpan timePassed = DateTime.UtcNow - unit.Date;
                    if (timePassed >= TimeSpan.FromDays(2))
                    {
                        storage.Remove(unit.Name);
                        modified = true;
                    }
                }
            }
            if (modified)
                storage.Serialize(loc);
        }
    }

}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/IvionSoft")]
public class TimeLeftUnit
{
    [DataMember]
    public string Name { get; private set; }
    [DataMember]
    public DateTime Date { get; set; }


    public TimeLeftUnit(string name, DateTime date)
    {
        Name = name;
        Date = date;
    }
}