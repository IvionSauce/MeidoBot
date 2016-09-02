﻿using System;
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
    }


    public TellsInbox Get(string nick)
    {
        TellsInbox inbox;
        if (nickToInbox.TryGetValue(nick, out inbox))
        {
            return inbox;
        }
        else
            return null;
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
}