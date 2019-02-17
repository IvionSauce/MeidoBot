using System;


public static class ExtensionMethods
{
    public static string Str(this TimeSpan duration)
    {
        var hours = Math.Abs((int)duration.TotalHours);
        var minutes = Math.Abs(duration.Minutes);
        int seconds = Math.Abs(duration.Seconds);

        if (duration >= TimeSpan.Zero)
            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        else
            return string.Format("-{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }
}