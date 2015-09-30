using System;
using System.Threading;
using System.Collections.Generic;


namespace MeidoBot
{
    public static class MeidoManager
    {
        static readonly Dictionary<string, Meido> runningBots =
            new Dictionary<string, Meido>(StringComparer.OrdinalIgnoreCase);
        
        static readonly Dictionary<string, MeidoConfig> configs =
            new Dictionary<string, MeidoConfig>(StringComparer.OrdinalIgnoreCase);

        static object _locker = new object();


        public static void StartBot(MeidoConfig config)
        {
            lock (_locker)
            {
                configs[config.ServerAddress] = config;
                StartBotThread(config);
            }
        }


        public static bool RestartBot(string server)
        {
            lock (_locker)
            {
                if (StopBot(server))
                {
                    StartBotThread(configs[server]);
                    return true;
                }
            }
            
            return false;
        }

        public static void RestartAllBots()
        {
            lock (_locker)
            {
                foreach (var pair in runningBots)
                {
                    pair.Value.Dispose();
                    StartBotThread(configs[pair.Key]);
                }
            }
        }


        public static bool StartBot(string server)
        {
            lock (_locker)
            {
                MeidoConfig config;
                if (configs.TryGetValue(server, out config))
                {
                    // If we have the config, but the bot is not running - start it.
                    if (!runningBots.ContainsKey(server))
                        StartBotThread(config);
                    
                    // Return true in any case, it's now running (regardless of whether it was running previously).
                    return true;
                }
            }
            return false;
        }

        static void StartBotThread(MeidoConfig config)
        {
            runningBots[config.ServerAddress] = new Meido(config);
            var t = new Thread(runningBots[config.ServerAddress].Connect);
            t.Start();
        }


        public static bool StopBot(string server)
        {
            lock (_locker)
            {
                Meido bot;
                if (runningBots.TryGetValue(server, out bot))
                {
                    bot.Dispose();
                    runningBots.Remove(server);

                    return true;
                }
            }
            return false;
        }

        public static void StopAllBots()
        {
            lock (_locker)
            {
                foreach (var pair in runningBots)
                {
                    pair.Value.Dispose();
                    runningBots.Remove(pair.Key);
                }
            }
        }


        public static string[] GetServers()
        {
            lock (_locker)
            {
                string[] servers = new string[runningBots.Count];

                int i = 0;
                foreach (var server in runningBots.Keys)
                {
                    servers[i] = server;
                    i++;
                }

                return servers;
            }
        }

    }
}