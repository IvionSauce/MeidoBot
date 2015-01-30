using System.Xml;
using System.Xml.Linq;
using MeidoCommon;


public class Config : XmlConfig
{
    public string WeatherUndergroundApiKey { get; set; }


    public Config(string file, ILog log) : base(file, log) {}

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