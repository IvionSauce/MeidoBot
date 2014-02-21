using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

[DataContract]
public class TimeLeftStorage
{
    [DataMember]
    public Dictionary<string, TimeLeftUnit> Items { get; set; }
    
    object _locker = new object();
    
    
    public TimeLeftStorage()
    {
        Items = new Dictionary<string, TimeLeftUnit>(StringComparer.OrdinalIgnoreCase);
    }
    
    public void Set(string name, DateTime date)
    {
        lock (_locker)
            Items[name] = new TimeLeftUnit(name, date);
    }
    
    public TimeLeftUnit Get(string name)
    {
        TimeLeftUnit unit;
        // Don't lock around this, since TryGetValue is atomic (inb4 lolitsnot).
        if (Items.TryGetValue(name, out unit))
            return unit;
        else
            return null;
    }

    public TimeLeftUnit[] GetAll()
    {
        lock (_locker)
        {
            var tluArr = new TimeLeftUnit[Items.Count];
            int i = 0;
            foreach (TimeLeftUnit tlu in Items.Values)
            {
                tluArr[i] = tlu;
                i++;
            }
            
            return tluArr;
        }
    }
    
    public TimeLeftUnit[] Search(string name)
    {
        var units = new List<TimeLeftUnit>();
        lock (_locker)
        {
            foreach (var pair in Items)
            {
                if (pair.Key.Contains(name, StringComparison.OrdinalIgnoreCase))
                    units.Add(pair.Value);
            }
        }
        
        return units.ToArray();
    }
    
    public bool Remove(string name)
    {
        lock (_locker)
            return Items.Remove(name);
    }
}


public class TimeLeftUnit
{
    public string Name { get; set; }
    public DateTime Date { get; set; }
    
    
    public TimeLeftUnit(string name, DateTime date)
    {
        Name = name;
        Date = date;
    }
}


static class ExtensionMethods
{
    public static bool Contains(this string source, string value, StringComparison comp)
    {
        return source.IndexOf(value, comp) >= 0;
    }
}