using System;
using System.Collections.Generic;
using System.Xml.Linq;
using MeidoCommon;


class Config
{
    public Uri Feed { get; set; }
    public TimeSpan Interval { get; set; }
    public HashSet<string> ActiveChannels { get; set; }
    public HashSet<string> SkipCategories { get; set; }
    
    
    public Config(XElement xml)
    {
        Uri feed;
        if ( Uri.TryCreate(xml.Element("feed").Value, UriKind.Absolute, out feed) )
        {
            Feed = feed;
        }

        var interval = (int)xml.Element("interval");
        if (interval < 1)
            Interval = TimeSpan.FromMinutes(15);
        else if (interval > 60)
            Interval = TimeSpan.FromMinutes(60);
        else
            Interval = TimeSpan.FromMinutes(interval);

        ActiveChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        XElement activeChannels = xml.Element("active-channels");
        if (activeChannels != null)
        {
            foreach (XElement chan in activeChannels.Elements())
            {
                if (!string.IsNullOrEmpty(chan.Value))
                    ActiveChannels.Add(chan.Value);
            }
        }

        SkipCategories = new HashSet<string>();
        XElement skipCategories = xml.Element("skip-categories");
        if (skipCategories != null)
        {
            foreach (XElement cat in skipCategories.Elements())
            {
                if (!string.IsNullOrEmpty(cat.Value))
                    SkipCategories.Add(cat.Value);
            }
        }
    }

    
    public static XElement DefaultConfig()
    {
        var config =
            new XElement ("config",
                          new XElement ("feed",
                                        new XComment ("Address of the RSS/Atom feed")),
                          new XElement ("interval", 15,
                                        new XComment ("In minutes")),
                          new XElement ("active-channels",
                                        new XElement ("channel", string.Empty)),
                          new XElement ("skip-categories",
                                        new XElement ("category", string.Empty))
                         );

        return config;
    }
}