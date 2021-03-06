using System;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

namespace MeidoBot
{
    // For information on using MEF, see:
    // http://dotnetbyexample.blogspot.nl/2010/04/very-basic-mef-sample-using-importmany.html and
    // http://msdn.microsoft.com/en-us/library/dd460648.aspx
    // Both were used by yours truly to make this very simple plugin-architecture you see below.
    class PluginManager : IDisposable
    {
        public int Count
        {
            get { return pluginContainer.Length; }
        }

        public event Action<MeidoPlugin> PluginLoad;


        [ImportMany(typeof(IMeidoHook))]
        IMeidoHook[] pluginContainer;


        public PluginManager()
        {
            pluginContainer = new IMeidoHook[0];
        }

        public void LoadPlugins(IIrcComm ircComm, IMeidoComm meidoComm)
        {
            // Create catalog and add our plugin-directory to it.
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new DirectoryCatalog("."));

            // Put it in a container and compose/import the plugins into the pluginContainer field above.
            using (var container = new CompositionContainer(catalog))
            {
                // With thanks to http://stackoverflow.com/questions/7684766/
                container.ComposeExportedValue(ircComm);
                container.ComposeExportedValue(meidoComm);

                container.ComposeParts(this);
            }
            OnPluginLoad();
        }

        public void OnPluginLoad()
        {
            if (PluginLoad != null)
            {
                foreach (var plugin in pluginContainer)
                {
                    PluginLoad(new MeidoPlugin(plugin));
                }
            }
        }


        public string[] GetDescriptions()
        {
            var descriptions = new string[Count];

            for (int i = 0; i < Count; i++)
            {
                var plugin = pluginContainer[i];
                descriptions[i] = plugin.Name() + " " + plugin.Version;
            }

            return descriptions;
        }


        public void Dispose()
        {
            foreach (var plugin in pluginContainer)
                plugin.Stop();
        }
    }
}