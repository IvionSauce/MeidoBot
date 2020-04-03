using System;
using System.Linq;
using System.Collections.Generic;


namespace MeidoCommon.Parsing
{
    [Flags]
    public enum JoinedOptions
    {
        None = 0,
        TrimInterior = 1,
        TrimExterior = 2,
        RemoveEmpty = 4,
        RemoveEmptyAndWhitespace = 8,

        TrimRemove = TrimInterior | RemoveEmpty
    }


    public static class StringJoinTools
    {
        public static string ToJoined(this IEnumerable<string> seq)
        {
            return ToJoined(seq, JoinedOptions.None);
        }

        public static string ToJoined(this IEnumerable<string> seq, JoinedOptions opts)
        {
            if (seq == null)
                throw new ArgumentNullException(nameof(seq));
            // Skip all the IEnumerable and branching lambda bullshit for the simple cases.
            if (opts == JoinedOptions.None)
                return string.Join(" ", seq);
            if (opts == JoinedOptions.TrimExterior)
                return string.Join(" ", seq).Trim();

            Func<string, string> trim;
            if (opts.HasFlag(JoinedOptions.TrimInterior))
                trim = s => s?.Trim();
            else
                trim = s => s;

            Func<string, bool> predicate;
            if (opts.HasFlag(JoinedOptions.RemoveEmptyAndWhitespace))
                predicate = s => !string.IsNullOrWhiteSpace(s);
            else if (opts.HasFlag(JoinedOptions.RemoveEmpty))
                predicate = s => !string.IsNullOrEmpty(s);
            else
                predicate = s => true;

            var tmp =
                from s in seq
                select trim(s) into trimmed
                where predicate(trimmed)
                select trimmed;

            var joined = string.Join(" ", tmp);

            if (opts.HasFlag(JoinedOptions.TrimExterior) && !opts.HasFlag(JoinedOptions.TrimRemove))
                return joined.Trim();
            else
                return joined;
        }
    }
}