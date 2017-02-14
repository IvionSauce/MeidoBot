using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Runtime.Serialization;


class Inboxes
{
    readonly Dictionary<string, TellsInbox> nickToInbox =
            new Dictionary<string, TellsInbox>(StringComparer.OrdinalIgnoreCase);

    readonly string storagePath;
    const string Prefix = "inbox-";

    static readonly DataContractSerializer dcs = new DataContractSerializer( typeof(TellsInbox) );


    public Inboxes(string storagePath)
    {
        this.storagePath = storagePath;
        LoadAll();
        Prune();
    }


    public TellsInbox Get(string nick)
    {
        TellsInbox inbox;
        if (nickToInbox.TryGetValue(nick, out inbox))
        {
            return inbox;
        }
        else
            return TellsInbox.Empty;
    }


    public TellsInbox GetOrNew(string nick)
    {
        TellsInbox inbox;
        if (!nickToInbox.TryGetValue(nick, out inbox))
        {
            inbox = new TellsInbox(nick);
            nickToInbox[nick] = inbox;
        }

        return inbox;
    }


    void LoadAll()
    {
        const string pattern = Prefix + "*.xml";

        var files = Directory.GetFiles(storagePath, pattern);
        foreach (var f in files)
        {
            Load(f);
        }
    }

    void Load(string file)
    {
        try
        {
            using (var stream = File.Open(file, FileMode.Open))
            {
                var inbox = (TellsInbox)dcs.ReadObject(stream);
                nickToInbox[inbox.Username] = inbox;
            }
        }
        catch (FileLoadException)
        {
            // Ignore not found files.
        }
    }


    public void Prune()
    {
        const int days = 60;
        var tSpan = TimeSpan.FromDays(days);
        var predicate = OlderThan(tSpan);

        var changed = new List<TellsInbox>();
        // Prune messages in each inbox.
        foreach (TellsInbox inbox in nickToInbox.Values)
        {
            if (inbox.DeleteMessages(predicate) > 0)
                changed.Add(inbox);
        }

        // Save changed inboxes.
        foreach (TellsInbox inbox in changed)
            Save(inbox);
    }

    static Func<TellEntry, bool> OlderThan(TimeSpan t)
    {
        var now = DateTime.UtcNow;

        return entry => {
            if (now - entry.SentDateUtc > t)
                return true;
            else
                return false;
        };
    }


    public void Save(string nick)
    {
        TellsInbox inbox;
        if (nickToInbox.TryGetValue(nick, out inbox))
        {
            Save(inbox);
        }
    }

    public void Save(TellsInbox inbox)
    {
        string filename =
            Prefix + inbox.Username.ToLowerInvariant() + ".xml";
        var path = Path.Combine(storagePath, filename);

        // Normal save.
        if (inbox.MessagesCount > 0)
        {
            using (var stream = File.Open(path, FileMode.Create))
            {
                var settings = new XmlWriterSettings() {
                    Indent = true,
                    // Because tell messages can contain control codes that make the XmlWriter barf,
                    // disable checking. Not ideal, but it seems to serialize and deserialize fine.
                    CheckCharacters = false
                };

                using (var writer = XmlWriter.Create(stream, settings))
                    dcs.WriteObject(writer, inbox);
            }
        }
        // Remove empty inbox from dict and clean up remnants of the inbox on disk.
        else
        {
            nickToInbox.Remove(inbox.Username);
            File.Delete(path);
        }
    }
}