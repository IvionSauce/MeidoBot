using System.Xml.Linq;


public class Config
{
    public string WeatherUndergroundApiKey { get; set; }


    public Config(XElement xml)
    {
        WeatherUndergroundApiKey = (string)xml.Element("wu-api-key") ?? string.Empty;
    }


    public static XElement DefaultConfig()
    {
        var config =
            new XElement("config", new XElement("wu-api-key", string.Empty));
        return config;
    }
}