using System;
using System.IO;
using System.Collections.Generic;


namespace MeidoBot
{
    class WatchConfig
    {
        readonly Logger log;
        readonly FileSystemWatcher watcher;

        readonly Dictionary<string, ConfigItem> trackedConfigs = new Dictionary<string, ConfigItem>();

        static readonly TimeSpan gracePeriod = TimeSpan.FromSeconds(2);


        public WatchConfig(string directory, Logger log)
        {
            watcher = new FileSystemWatcher(directory);
            this.log = log;

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRename;
            watcher.EnableRaisingEvents = true;
        }


        void OnChanged(object sender, FileSystemEventArgs e)
        {
            ConfigChange(e.Name, e.FullPath);
        }
        void OnRename(object sender, RenamedEventArgs e)
        {
            ConfigChange(e.Name, e.FullPath);
        }

        void ConfigChange(string filename, string fullPath)
        {
            var conf = Get(filename);

            if (conf != null)
            {
                var now = DateTimeOffset.Now;
                lock (conf)
                {
                    if ((now - conf.PreviousLoad) > gracePeriod)
                    {
                        log.Message("Detected change in '{0}', reloading configuration...", filename);
                        conf.Load(fullPath);
                        conf.PreviousLoad = now;
                    }
                }
            }
        }


        public void LoadAndWatch(string filename, Action<string> loadConfig)
        {
            // Load
            string path = Path.Combine(watcher.Path, filename);
            loadConfig(path);

            // Watch
            ConfigItem conf;
            lock (trackedConfigs)
            {
                conf = Get(filename);
                if (conf == null)
                {
                    trackedConfigs[filename] = new ConfigItem(loadConfig);
                    return;
                }
            }

            lock (conf)
            {
                conf.Load += loadConfig;
            }
        }


        ConfigItem Get(string filename)
        {
            lock (trackedConfigs)
            {
                ConfigItem conf;
                if (trackedConfigs.TryGetValue(filename, out conf))
                {
                    return conf;
                }
            }

            return null;
        }
    }


    class ConfigItem
    {
        public Action<string> Load { get; set; }
        public DateTimeOffset PreviousLoad { get; set; }


        public ConfigItem(Action<string> load)
        {
            Load = load;
            PreviousLoad = DateTimeOffset.Now;
        }
    }
}