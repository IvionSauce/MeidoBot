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





        public static bool StartBot(string server)
        {
            return StartBot(server, false);
        }

        public static bool StartBot(string server, bool restart)
        {
            lock (_locker)
            {
                MeidoConfig config;
                if (configs.TryGetValue(server, out config))
                    return StartBot(config, restart);
            }
            return false;
        }


        public static bool StartBot(MeidoConfig config)
        {
            return StartBot(config, false);
        }

        public static bool StartBot(MeidoConfig config, bool restart)
        {
            lock (_locker)
            {
                if (!runningBots.ContainsKey(config.ServerAddress))
                {
                    AddOrReplaceConfig(config);
                    StartBotThread(config);
                    return true;
                }
                else if (restart)
                {
                    AddOrReplaceConfig(config);
                    RestartBot(config.ServerAddress);
                    return true;
                }
            }
            return false;
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


        public static bool AddConfig(MeidoConfig conf)
        {
            lock (_locker)
            {
                if (!configs.ContainsKey(conf.ServerAddress))
                {
                    configs[conf.ServerAddress] = conf;
                    return true;
                }
            }
            return false;
        }

        public static void AddOrReplaceConfig(MeidoConfig conf)
        {
            lock (_locker)
            {
                configs[conf.ServerAddress] = conf;
            }
        }


        public static bool IsBotStarted(string server)
        {
            lock (_locker)
            {
                return runningBots.ContainsKey(server);
            }
        }

        public static bool HasConfig(string server)
        {
            lock (_locker)
            {
                return configs.ContainsKey(server);
            }
        }


        public static void Clear()
        {
            lock (_locker)
            {
                StopAllBots();
                configs.Clear();
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