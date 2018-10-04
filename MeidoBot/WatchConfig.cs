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

        // Creating or writing to a file often raises multiple Created/Changed events, so have a grace period
        // wherein we ignore events for a file.
        static readonly TimeSpan gracePeriod = TimeSpan.FromSeconds(2);


        public WatchConfig(string directory, Logger log)
        {
            watcher = new FileSystemWatcher(directory);
            this.log = log;

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            // Without explicitly setting this filter to "*" (all files) it will only raise events for
            // filenames containing a dot, ie with an extension. Whether this setting works and how it works
            // _might_ be platform specific.
            watcher.Filter = "*";

            watcher.Created += OnChange;
            watcher.Changed += OnChange;
            watcher.Renamed += OnRename;
            watcher.EnableRaisingEvents = true;
        }


        void OnChange(object sender, FileSystemEventArgs e)
        {
            //log.Verbose("OnChange ({0}) for {1}", e.ChangeType, e.Name);
            ConfigChange(e.Name, e.FullPath);
        }
        void OnRename(object sender, RenamedEventArgs e)
        {
            //log.Verbose("OnRename ({0}) for {1} -> {2}", e.ChangeType, e.OldName, e.Name);
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