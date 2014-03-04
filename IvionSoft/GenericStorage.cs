using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization;


namespace IvionSoft
{
    [DataContract]
    public class Storage<T>
    {
        [DataMember]
        public Dictionary<string, T> Items { get; private set; }
        [DataMember]
        public T DefaultValue { get; set; }

        static DataContractSerializer dcs = new DataContractSerializer( typeof(Storage<T>) );


        public Storage()
        {
            Items = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            DefaultValue = default(T);
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            // Sadly serializing doesn't write the EqualityComparer, so when deserializing it will initiate the
            // dictionary with the wrong comparer. So we can't avoid this O(n) cost.
            Items = new Dictionary<string, T>(Items, StringComparer.OrdinalIgnoreCase);
        }


        public void Set(string id, T item)
        {
            Items[id] = item;
        }

        public bool Contains(string id)
        {
            return Items.ContainsKey(id);
        }

        public T Get(string id)
        {
            return Get(id, DefaultValue);
        }

        public T Get(string id, T defaultValue)
        {
            T item;
            if (Items.TryGetValue(id, out item))
                return item;
            else
                return defaultValue;
        }
        
        public IEnumerable<T> GetAll()
        {
            foreach (T item in Items.Values)
                yield return item;
        }
        
        public IEnumerable<T> Search(string partialId)
        {
            foreach (var pair in Items)
                if (pair.Key.Contains(partialId, StringComparison.OrdinalIgnoreCase))
                    yield return pair.Value;
        }
        
        public bool Remove(string id)
        {
            return Items.Remove(id);
        }


        public void Serialize(string path)
        {
            Serialize(this, path);
        }


        static public void Serialize(Storage<T> storage, string path)
        {
            var settings = new XmlWriterSettings() { Indent = true };
            
            using (var stream = File.Open(path, FileMode.Create))
                using (var writer = XmlWriter.Create(stream, settings))
                    dcs.WriteObject(writer, storage);
        }

        static public Storage<T> Deserialize(string path)
        {
            using (var stream = File.Open(path, FileMode.Open))
                   return (Storage<T>)dcs.ReadObject(stream);
        }
    }
}