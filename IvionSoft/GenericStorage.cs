using System;
using System.IO;
using System.Xml;
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


        public Storage() : this(default(T)) {}

        public Storage(T defaultValue)
        {
            Items = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            DefaultValue = defaultValue;
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
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            Items[id] = item;
        }

        public bool Contains(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            
            return Items.ContainsKey(id);
        }


        public T GetOrSet(string id, Func<T> itemGenerator)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (itemGenerator == null)
                throw new ArgumentNullException(nameof(itemGenerator));

            T item;
            if (!Items.TryGetValue(id, out item))
            {
                item = itemGenerator();
                Items[id] = item;
            }

            return item;
        }


        public T Get(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            
            return Get(id, DefaultValue);
        }

        public T Get(string id, T defaultValue)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            
            T item;
            if (Items.TryGetValue(id, out item))
                return item;
            else
                return defaultValue;
        }


        public T[] GetAll()
        {
            var items = new T[Items.Count];
            int i = 0;
            foreach (var item in Items.Values)
            {
                items[i] = item;
                i++;
            }

            return items;
        }
        
        public IEnumerable<T> Search(string partialId)
        {
            if (partialId == null)
                throw new ArgumentNullException(nameof(partialId));
            
            foreach (var pair in Items)
            {
                if (pair.Key.Contains(partialId, StringComparison.OrdinalIgnoreCase))
                    yield return pair.Value;
            }
        }


        public bool Remove(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            
            return Items.Remove(id);
        }


        public void RemoveAll(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            
            foreach (var pair in Items)
            {
                if (predicate(pair.Value))
                    Items.Remove(pair.Key);
            }
        }


        public void Serialize(string path)
        {
            Serialize(this, path);
        }


        static public void Serialize(Storage<T> storage, string path)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            path.ThrowIfNullOrWhiteSpace(nameof(path));

            var settings = new XmlWriterSettings() { Indent = true };
            
            using (var stream = File.Open(path, FileMode.Create))
            {
                using (var writer = XmlWriter.Create(stream, settings))
                    dcs.WriteObject(writer, storage);
            }
        }

        static public Storage<T> Deserialize(string path)
        {
            path.ThrowIfNullOrWhiteSpace(nameof(path));

            using (var stream = File.Open(path, FileMode.Open))
                   return (Storage<T>)dcs.ReadObject(stream);
        }
    }
}