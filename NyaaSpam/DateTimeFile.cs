using System;
using System.IO;
using System.Globalization;


class DateTimeFile
{
    readonly string path;

    const string dateFmt = "o";


    public DateTimeFile (string path)
    {
        this.path = path;
    }


    public void Write(DateTimeOffset date)
    {
        Write(date, path);
    }

    public DateTimeOffset Read()
    {
        return Read(path);
    }


    public static void Write(DateTimeOffset date, string path)
    {
        string dateStr = date.ToString(dateFmt);
        try
        {
            File.WriteAllText(path, dateStr);
        }
        catch (IOException)
        {}
    }

    public static DateTimeOffset Read(string path)
    {
        DateTimeOffset date = DateTimeOffset.MinValue;
        try
        {
            string dateStr = File.ReadAllText(path);
            date = DateTimeOffset.ParseExact(dateStr, dateFmt, CultureInfo.InvariantCulture);
        }
        catch (IOException)
        {}
        catch (FormatException)
        {}

        return date;
    }

    public static DateTimeOffset SanityCheck(DateTimeOffset pastDate, TimeSpan maxElapsed)
    {
        var now = DateTimeOffset.Now;

        if (pastDate < now &&
            (now - pastDate) <= maxElapsed)
        {
            return pastDate;
        }

        return now;
    }
}