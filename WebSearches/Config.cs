using System.Xml;
using System.Xml.Linq;
using IvionSoft;


public class Config : XmlConfig
{
    public string WeatherUndergroundApiKey { get; set; }


    public Config(string file) : base(file) {}

    public override void LoadConfig()
    {
        WeatherUndergroundApiKey = (string)Config.Element("wu-api-key") ?? string.Empty;
    }

    public override XElement DefaultConfig()
    {
        var config =
            new XElement("config", new XElement("wu-api-key", string.Empty));
        return config;
    }
}