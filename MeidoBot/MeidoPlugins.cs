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
    class PluginManager
    {
        [ImportMany(typeof(IMeidoHook))]
        IEnumerable<IMeidoHook> pluginContainer;

        public int Count { get; private set; }
        public string Prefix { get; set; }


        public void LoadPlugins(IIrcComm ircComm, IMeidoComm meidoComm)
        {
            // Create catalog and add our plugin-directory to it.
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new DirectoryCatalog("."));

            // Put it in a container and compose/import the plugins into the IEnumerable pluginContainer field above.
            var container = new CompositionContainer(catalog);

            // With thanks to http://stackoverflow.com/questions/7684766/
            container.ComposeExportedValue<IIrcComm>(ircComm);
            container.ComposeExportedValue<IMeidoComm>(meidoComm);

            container.ComposeParts(this);

            // Count the number of plugins loaded and make it availabe in the Count property.
            // Also share the prefix with them.
            int count = 0;
            foreach (var plugin in pluginContainer)
            {
                plugin.Prefix = this.Prefix;
                count++;
            }
            Count = count;
        }

        public string[] GetDescriptions()
        {
            var descriptions = new string[Count];

            int i = 0;
            foreach (IMeidoHook plugin in pluginContainer)
            {
                descriptions[i] = string.Concat(plugin.Name, " ", plugin.Version);
                i++;
            }

            return descriptions;
        }

        public string[] GetHelpSubjects()
        {
            var keys = new List<string>();
            foreach (IMeidoHook plugin in pluginContainer)
                foreach (string key in plugin.Help.Keys)
                    keys.Add(key);

            keys.Sort();
            return keys.ToArray();
        }

        public string GetHelp(string subject)
        {
            string helpSubject;
            if (subject.StartsWith(Prefix))
                helpSubject = subject.Substring(1);
            else
                helpSubject = subject;

            string help;
            foreach (IMeidoHook plugin in pluginContainer)
                if (plugin.Help.TryGetValue(helpSubject, out help))
                    return help;

            return null;
        }
    }
}