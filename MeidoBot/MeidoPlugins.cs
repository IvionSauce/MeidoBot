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
    public class PluginManager
    {
        [ImportMany(typeof(IMeidoHook))]
        IEnumerable<IMeidoHook> pluginContainer;

        public int Count { get; private set; }

        string _triggerPrefix = ".";
        public string TriggerPrefix
        {
            get { return _triggerPrefix; }
            set { _triggerPrefix = value; }
        }


        public void LoadPlugins(IIrcComm ircComm)
        {
            // Create catalog and add our plugin-directory to it.
            var catalog = new AggregateCatalog();
            // catalog.Catalogs.Add(new AssemblyCatalog(typeof(PluginManager).Assembly));
            catalog.Catalogs.Add(new DirectoryCatalog("."));

            // Put it in a container and compose/import the plugins into the IEnumerable pluginContainer field above.
            var container = new CompositionContainer(catalog);

            // With thanks to http://stackoverflow.com/questions/7684766/
            container.ComposeExportedValue<IIrcComm>(ircComm);

            container.ComposeParts(this);

            // Count the number of plugins loaded and make it availabe in the Count property.
            int count = 0;
            foreach (var plugin in pluginContainer)
                count++;
            Count = count;
        }

        public string[] GetDescriptions()
        {
            var descriptions = new string[Count];

            int i = 0;
            foreach (IMeidoHook plugin in pluginContainer)
            {
                descriptions[i] = plugin.Description;
                i++;
            }

            return descriptions;
        }

        public string[] GetHelpSubjects()
        {
            var keys = new List<string>();
            foreach (IMeidoHook plugin in pluginContainer)
                foreach (string key in plugin.exportedHelp.Keys)
                    keys.Add(key);

            keys.Sort();
            return keys.ToArray();
        }

        public string GetHelp(string subject)
        {
            string subjectWithPrefix = TriggerPrefix + subject;
            string help;

            foreach (IMeidoHook plugin in pluginContainer)
            {
                if (plugin.exportedHelp.TryGetValue(subject, out help) ||
                    plugin.exportedHelp.TryGetValue(subjectWithPrefix, out help))
                    return help;
            }
            return null;
        }
    }
}