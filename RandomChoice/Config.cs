using System.Collections.Generic;
using System.Xml.Linq;


class Config
{
    public List<string> LaunchChoices { get; set; }


    public Config(XElement xml)
    {
        XElement countdownOptions = xml.Element("countdown");
        LaunchChoices = new List<string>();

        if (countdownOptions != null)
        {
            foreach (XElement option in countdownOptions.Elements())
            {
                if (!string.IsNullOrWhiteSpace(option.Value))
                    LaunchChoices.Add(option.Value);
            }
        }
    }

    public static XElement DefaultConfig()
    {
        var config = 
            new XElement("config",
                         new XElement("countdown",
                                      new XElement("option", "Launch!"),
                                      new XElement("option", "Hasshin!"),
                                      new XElement("option", "Gasshin!"),
                                      new XElement("option", "Gattai!"),
                                      new XElement("option", "Rider Kick!"),
                                      new XElement("option", "Clock Up!"),
                                      new XElement("option", "Are you Ready? We are l@dy!"),
                                      new XElement("option", "Heaven or Hell!"),
                                      new XElement("option", "Let's Rock!"),
                                      new XElement("option", "Apprivoise!!"),
                                      new XElement("option", "Kiraboshi!"),
                                      new XElement("option", "Fight!")
                                     )
                        );
        return config;
    }
}