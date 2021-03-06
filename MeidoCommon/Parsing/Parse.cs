﻿using System;
using System.Text.RegularExpressions;


namespace MeidoCommon.Parsing
{
    public static class Parse
    {
        public static TimeSpan ShortTimeString(string shortTime)
        {
            if (shortTime == null)
                throw new ArgumentNullException(nameof(shortTime));
            
            var timeRegexp = new Regex(
                @"^\s*([+-])?\s*
                (?:(\d*\.?\d+)h\s*)? 
                (?:(\d*\.?\d+)m\s*)? 
                (?:(\d*\.?\d+)s\s*)?$",
                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace
            );

            var m = timeRegexp.Match(shortTime);
            var signGrp = m.Groups[1];
            var hourGrp = m.Groups[2];
            var minuteGrp = m.Groups[3];
            var secondGrp = m.Groups[4];

            double hours = 0;
            double minutes = 0;
            double seconds = 0;

            if (hourGrp.Success)
                hours = double.Parse(hourGrp.Value);
            if (minuteGrp.Success)
                minutes = double.Parse(minuteGrp.Value);
            if (secondGrp.Success)
                seconds = double.Parse(secondGrp.Value);

            var ts = TimeSpan.FromHours(hours) +
                     TimeSpan.FromMinutes(minutes) +
                     TimeSpan.FromSeconds(seconds);

            if (signGrp.Value == "-")
                ts = TimeSpan.Zero - ts;

            return ts;
        }


        public static bool StartsWithTriggerPrefix(this IIrcMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));
            
            return StartsWithTriggerPrefix(msg.Message);
        }

        public static bool StartsWithTriggerPrefix(this string message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (message.Length > 0)
            {
                // Record of trigger prefixes I've seen in the wild.
                switch (message[0])
                {
                    case '.':
                    // Special exception for dot, since they're commonly
                    // used as ellipsis.
                    if (message.Length > 1 && message[1] == '.')
                        break;
                    return true;

                    case '!':
                    case '@':
                    case '~':
                    return true;
                }
            }

            return false;
        }
    }
}