using System;
using System.IO;
using IvionWebSoft;
using WebIrc;
using MeidoCommon;
using MeidoCommon.Parsing;


class QueryTriggers
{
    volatile Config config;


    public void Configure(Config conf)
    {
        config = conf;
    }


    public void Query(ITriggerMsg msg, bool debug)
    {
        WebToIrc wIrc;
        if (msg.Channel != null)
            wIrc = config.ConstructWebToIrc(msg.Channel);
        else
            wIrc = config.ConstructWebToIrc("_all");

        foreach (var arg in msg.Arguments)
        {
            var result = wIrc.WebInfo(arg);

            if (debug)
            {
                foreach (string s in result.Messages)
                    msg.Reply(s);
            }

            if (result.Success)
                msg.Reply(result.Title);
            else
                msg.Reply(result.Exception.Message);
        }
    }


    public static void Dump(ITriggerMsg msg)
    {
        foreach (var arg in msg.Arguments)
        {
            if ( Uri.TryCreate(arg, UriKind.Absolute, out Uri url) )
            {
                msg.Reply(Dump(url));
            }
        }
    }

    static string Dump(Uri url)
    {
        var fetcher = new WebUriFetcher();
        var result = fetcher.Load(url);

        if (result.IsHtml)
        {
            try
            {
                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, result.Page.Content);

                return string.Format("Successfully dumped HTML contents of {0} to {1}", url, tmp);
            }
            catch (IOException ex)
            {
                return ex.Message;
            }
        }
        else
        {
            if (result.Bytes.Success)
                return string.Format("Not HTML ({0}): {1}", result.Bytes.ContentType, url);
            else
                return result.Bytes.Exception.Message;
        }
    }

}