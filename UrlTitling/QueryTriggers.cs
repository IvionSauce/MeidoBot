using System;
using System.IO;
using System.Threading;

using IvionWebSoft;
using WebIrc;
using MeidoCommon;

class QueryTriggers
{
    Config config;


    public QueryTriggers(Config conf)
    {
        config = conf;
    }


    public void Query(IIrcMessage msg)
    {
        ThreadPool.QueueUserWorkItem( (cb) => Query(msg, false) );
    }
    public void QueryDebug(IIrcMessage msg)
    {
        ThreadPool.QueueUserWorkItem( (cb) => Query(msg, true) );
    }

    void Query(IIrcMessage msg, bool debug)
    {
        WebToIrc wIrc;
        if (msg.Channel != null)
            wIrc = config.ConstructWebToIrc(msg.Channel);
        else
            wIrc = config.ConstructWebToIrc("_all");

        for (int i = 1; i < msg.MessageArray.Length; i++)
        {
            var result = wIrc.WebInfo(msg.MessageArray[i]);

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


    public static void Dump(IIrcMessage msg)
    {
        for (int i = 1; i < msg.MessageArray.Length; i++)
        {
            Uri url;
            if ( Uri.TryCreate(msg.MessageArray[i], UriKind.Absolute, out url) )
            {
                ThreadPool.QueueUserWorkItem( (cb) =>
                    msg.Reply(Dump(url)) );
            }
        }
    }

    static string Dump(Uri url)
    {
        var follower = new MetaRefreshFollower();
        var result = follower.Load(url);

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