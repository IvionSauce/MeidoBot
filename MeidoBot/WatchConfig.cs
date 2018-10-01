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
            ConfigItem config = null;
            lock (trackedConfigs)
            {
                trackedConfigs.TryGetValue(filename, out config);
            }

            if (config != null)
            {
                lock (config)
                {
                    var now = DateTimeOffset.Now;
                    if ((now - config.PreviousLoad) > gracePeriod)
                    {
                        config.Load(fullPath);
                        config.PreviousLoad = now;
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
            lock (trackedConfigs)
            {
                trackedConfigs[filename] = new ConfigItem(loadConfig);
            }
        }
    }


    class ConfigItem
    {
        public Action<string> Load { get; private set; }
        public DateTimeOffset PreviousLoad { get; set; }


        public ConfigItem(Action<string> load)
        {
            Load = load;
            PreviousLoad = DateTimeOffset.Now;
        }
    }
}