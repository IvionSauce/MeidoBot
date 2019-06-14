using System.Xml.Linq;


public class Config
{
    public string OpenWeatherMapApiKey { get; set; }


    public Config(XElement xml)
    {
        OpenWeatherMapApiKey = (string)xml.Element("owm-api-key") ?? string.Empty;
    }


    public static XElement DefaultConfig()
    {
        var config =
            new XElement("config", new XElement("owm-api-key", string.Empty));
        return config;
    }
}