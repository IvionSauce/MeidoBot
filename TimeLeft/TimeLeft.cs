using System;
using System.IO;
using System.Xml;
using System.Threading;
using System.Runtime.Serialization;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class TimeLeft : IMeidoHook
{
    IIrcComm irc;

    TimeLeftStorage storage;
    // Timer tmr;

    DataContractSerializer dcs = new DataContractSerializer( typeof(TimeLeftStorage) );
    const string loc = "conf/_timeleft.xml";
    
    public string Prefix { get; set; }
    
    public string Name
    {
        get { return "TimeLeft"; }
    }
    public string Version
    {
        get { return "0.20"; }
    }
    
    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"timeleft set", "timeleft set <name> <date> - Date must be in YYYY-MM-DD. Will replace entry if" +
                    "already exists."},
                {"timeleft del", "timeleft del <name> - Removes exact name (case insensitive) from tracking."},
                {"timeleft", "timeleft [name] - Check on the timeleft of name, name doesn't have to be exact. " +
                    "If no name is specified, will show all currently tracking names."}
            };
        }
    }
    
    [ImportingConstructor]
    public TimeLeft(IIrcComm ircComm)
    {
        storage = new TimeLeftStorage();

        try
        {
            using (var stream = File.Open(loc, FileMode.Open))
                storage = (TimeLeftStorage)dcs.ReadObject(stream);
        }
        catch (FileNotFoundException)
        {
            File.Create(loc).Dispose();
        }
        catch (XmlException)
        {}

        new Timer(Cleaner, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }
    
    public void HandleChannelMessage(IIrcMessage e)
    {
        if (e.MessageArray[0] == Prefix + "timeleft")
        {
            if ( e.MessageArray.Length >= 4 && (e.MessageArray[1] == "add" || e.MessageArray[1] == "set") )
            {
                Set(e.Nick, e.MessageArray);
            }

            else if (e.MessageArray.Length >= 3 && e.MessageArray[1] == "del")
            {
                Del(e.Nick, e.MessageArray);
            }

            else if (e.MessageArray.Length > 1)
            {
                var name = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
                Show(e.Channel, name);
            }

            else
            {
                foreach ( TimeLeftUnit unit in storage.GetAll() )
                    SendTime(e.Channel, unit.Name, unit.Date);
            }
        }
    }

    void Set(string nick, string[] message)
    {
        string[] dateArr = message[message.Length - 1].Split('-');
        if (dateArr.Length != 3)
            return;
        
        int year, month, day;
        if (int.TryParse( dateArr[0], out year ) &&
            int.TryParse( dateArr[1], out month ) &&
            int.TryParse( dateArr[2], out day ))
        {
            DateTime date;
            try
            {
                date = new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }
            // Subtract 3 from count, since we don't want the final argument - the date.
            var name = string.Join(" ", message, 2, message.Length - 3);
            
            storage.Set(name, date);
            WriteFile();

            irc.SendNotice( nick, string.Format("Set \"{0}\" :: {1}", name, date.ToString("MMMM dd, yyyy")) );
        }
    }

    void Del(string nick, string[] message)
    {
        var name = string.Join(" ", message, 2, message.Length - 2);
        if (storage.Remove(name))
        {
            WriteFile();
            irc.SendNotice( nick, string.Format("Deleted: {0}", name) );
        }
    }

    void Show(string channel, string name)
    {
        TimeLeftUnit unit = storage.Get(name);
        if (unit != null)
            SendTime(channel, unit.Name, unit.Date);
        // If no exact match, try to search for it.
        else
        {
            foreach (TimeLeftUnit tlu in storage.Search(name))
                SendTime(channel, tlu.Name, tlu.Date);
        }
    }

    void SendTime(string channel, string name, DateTime date)
    {
        TimeSpan timeLeft = date - DateTime.UtcNow;
        var message = string.Format("Days: {0}, Hours: {1} :: {2}", timeLeft.Days, timeLeft.Hours, name);
        
        irc.SendMessage(channel, message);
    }

    void Cleaner(object data)
    {
        foreach ( TimeLeftUnit unit in storage.GetAll() )
        {
            if (DateTime.UtcNow > unit.Date)
            {
                TimeSpan timePassed = DateTime.UtcNow - unit.Date;
                if (timePassed >= TimeSpan.FromDays(2))
                    storage.Remove(unit.Name);
            }
        }
        WriteFile();
    }

    void WriteFile()
    {
        var settings = new XmlWriterSettings() { Indent = true };

        using (var stream = File.Open(loc, FileMode.Open))
        using (var writer = XmlWriter.Create(stream, settings))
            dcs.WriteObject(writer, storage);
    }
}