using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using MeidoCommon;


class Config : XmlConfig
{
    public HashSet<string> ActiveChannels { get; set; }

    public int Interval { get; set; }
    public HashSet<string> SkipCategories { get; set; }
    
    
    public Config(string file, ILog log) : base(file, log) {}
    
    public override void LoadConfig()
    {
        ActiveChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        XElement activeChannels = Config.Element("active-channels");
        if (activeChannels != null)
        {
            foreach (XElement chan in activeChannels.Elements())
            {
                if (!string.IsNullOrEmpty(chan.Value))
                    ActiveChannels.Add(chan.Value);
            }
        }

        Interval = (int)Config.Element("interval");
        
        SkipCategories = new HashSet<string>();
        XElement skipCategories = Config.Element("skip-categories");
        if (skipCategories != null)
        {
            foreach (XElement cat in skipCategories.Elements())
            {
                if (!string.IsNullOrEmpty(cat.Value))
                    SkipCategories.Add(cat.Value);
            }
        }
    }
    
    public override XElement DefaultConfig()
    {
        var config =
            new XElement ("config",
                new XElement ("active-channels",
                    new XElement ("channel", string.Empty)
                ),

                new XElement ("interval", 15, new XComment ("In minutes")),

                new XElement ("skip-categories",
                    new XElement ("category", "Non-English-translated Anime"),
                    new XElement ("category", "Non-English-translated Live Action"),
                    new XElement ("category", "Non-English-scanlated Books")
                )
            );

        return config;
    }
}