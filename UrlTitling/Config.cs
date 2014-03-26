using System.Net;
using System.Collections.Generic;
using System.Xml.Linq;
using IvionSoft;
using WebIrc;

// Container for storing the various settings.
class WebToIrcConfig
{
    public int? MaxTags { get; set; }
    public string DanboContSym { get; set; }
    public bool? Colourize { get; set; }
    
    public int? MaxLines { get; set; }
    public int? MaxCharacters { get; set; }
    public string ChanContSym { get; set; }
}


class Config : XmlConfig
{
    // Global and/or static settings.
    public double Threshold { get; set; }
    public string BlacklistLocation { get; set; }
    public string WhitelistLocation { get; set; }
    public CookieCollection CookieColl { get; set; }
    
    // Channel specific settings, or at least the possibility thereof.
    // { channel : SettingsClass }
    public Dictionary<string, WebToIrcConfig> WebIrcSettings { get; private set; }
    
    
    public Config(string file) : base(file)
    {}
    
    public WebToIrc ConstructWebToIrc(string channel)
    {
        var webIrc = new WebToIrc();
        var global = GetChannelConfig("_all");
        var specific = GetChannelConfig(channel);
        
        // What follows are necesary if/else's for allowing a cascading config between global and channel specific
        // settings, as well as falling back to each properties default values if neither the global or channel specific
        // setting is set.
        
        // --- Danbooru ---
        // ----------------
        
        // Max Tag Count (for shortening characters, copyrights and artist tags).
        webIrc.Danbo.MaxTagCount =
            specific.MaxTags ?? global.MaxTags ?? 5;

        // Continuation Symbol.
        webIrc.Danbo.ContinuationSymbol =
            specific.DanboContSym ?? global.DanboContSym ?? string.Empty;

        // Colourize.
        webIrc.Danbo.Colourize = 
            specific.Colourize ?? global.Colourize ?? false;
        
        // --- 4chan & Foolz ---
        // ---------------------
        
        // Max Lines (for shortening the post).
        webIrc.Chan.TopicMaxLines =
            specific.MaxLines ?? global.MaxLines ?? 2;

        // Max Characters (for shortening the post).
        webIrc.Chan.TopicMaxChars =
            specific.MaxCharacters ?? global.MaxCharacters ?? 128;

        // Continuation Symbol.
        webIrc.Chan.ContinuationSymbol =
            specific.ChanContSym ?? global.ChanContSym ?? string.Empty;
        
        return webIrc;
    }
    
    public override void LoadConfig ()
    {
        Threshold = (double)Config.Element("threshold");
        BlacklistLocation = (string)Config.Element("blacklist-location");
        WhitelistLocation = (string)Config.Element("whitelist-location");
        
        LoadCookies();
        LoadIntoWebIrcSettings();
    }
    
    void LoadCookies()
    {
        CookieColl = new CookieCollection();
        XElement cookies = Config.Element("cookies");
        if (cookies == null)
            return;
        foreach (XElement cookie in cookies.Elements())
        {
            if (cookie == null)
                continue;
            
            var name = (string)cookie.Element("name");
            var content = (string)cookie.Element("content");
            var path = (string)cookie.Element("path");
            var host = (string)cookie.Element("host");
            
            if (!string.IsNullOrEmpty(name) &&
                !string.IsNullOrEmpty(content) &&
                !string.IsNullOrEmpty(path) &&
                !string.IsNullOrEmpty(host))
                CookieColl.Add(new Cookie(name, content, path, host));
        }
    }
    
    void LoadIntoWebIrcSettings()
    {
        WebIrcSettings = new Dictionary<string, WebToIrcConfig>();
        
        foreach (XElement danbo in Config.Elements("danbooru"))
        {
            if (danbo.HasElements == false)
                continue;
            
            var settings = GetChannelConfig( GetChannelAttr(danbo) );
            
            settings.MaxTags = (int?)danbo.Element("max-tags-displayed");
            settings.DanboContSym = (string)danbo.Element("continuation-symbol");
            settings.Colourize = (bool?)danbo.Element("colourize");
        }
        
        foreach (XElement chan in Config.Elements("chan-foolz"))
        {
            if (chan.HasElements == false)
                continue;
            
            var settings = GetChannelConfig( GetChannelAttr(chan) );
            
            settings.MaxLines = (int?)chan.Element("max-lines");
            settings.MaxCharacters = (int?)chan.Element("max-characters");
            settings.ChanContSym = (string)chan.Element("continuation-symbol");
        }
    }
    
    string GetChannelAttr(XElement el)
    {
        var channel = (string)el.Attribute("channel");
        if (string.IsNullOrEmpty(channel))
            return "_all";
        else
            return channel;
    }
    
    WebToIrcConfig GetChannelConfig(string channel)
    {
        string chanLow = channel.ToLower();
        
        WebToIrcConfig config;
        if (WebIrcSettings.TryGetValue(chanLow, out config))
            return config;
        else
        {
            WebIrcSettings.Add(chanLow, new WebToIrcConfig());
            return WebIrcSettings[chanLow];
        }
    }
    
    public override XElement DefaultConfig ()
    {
        var config = 
            new XElement("config",
                         new XElement("threshold", 1.0d, new XComment("Number between 0 and 1")),
                         new XElement("blacklist-location", "conf/blacklist"),
                         new XElement("whitelist-location", "conf/whitelist"),
                         
                         new XComment("For 'danbooru' and 'chan-foolz' you can have channel specific options.\n" +
                         "Just create another one with appropriate value in the `channel` attribute."),
                         
                         new XElement("danbooru", new XAttribute("channel", "_all"),
                            new XElement("max-tags-displayed", 5, new XComment("Limits each tag category " +
                                                               "(characters, copyrights and artists) to this number")),
                            new XElement("continuation-symbol", "[...]", new XComment("What to print to indicate " +
                                                                      "that there are more tags than displayed")),
                            new XElement("colourize", true)
                         /* new XElement("normal-code", "", new XComment("Default is no control-codes")),
                            new XElement("characters-code", "\u000303"),
                            new XElement("copyrights-code", "\u000306"),
                            new XElement("artists-code", "\u000305") */
                            ),
                         
                         new XElement("chan-foolz", new XAttribute("channel", "_all"),
                            new XElement("max-lines", 2),
                            new XElement("max-characters", 128),
                            new XElement("continuation-symbol", "…")
                            ),
                         
                         new XElement("cookies",
                            new XElement("cookie",
                                new XElement("name", ""),
                                new XElement("content", ""),
                                new XElement("path", ""),
                                new XElement("host", "")
                                )
                            )
                         );
        return config;
    }
}