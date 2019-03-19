using System;
using System.Globalization;
using System.Collections.Generic;


namespace MeidoBot
{
    static class MessageTools
    {
        public static bool IsChannel(string destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (destination == string.Empty)
                return false;
            
            char prefix = destination[0];
            switch (prefix)
            {
                // According to RFC2811:
                case '#': // standard channel
                case '&': // channel local to server
                case '+': // no channel modes
                case '!': // safe channel
                    return true;
                default:
                    return false;
            }
        }
    }


    // It should be noted that the 512 character limit for IRC messages means 512 bytes/octets. This is why we
    // need to look up how many bytes a certain character needs in UTF-8 (the encoding we use to send the messages).

    // Besides that, Unicode has graphemes: which are 1 visual character, but are made out of multiple concrete
    // characters/codepoints. We want to cut at grapheme boundaries, so we'll jump graphemes instead of chars.

    class MessageSplit
    {
        readonly string message;
        readonly int[] utf8Widths;
        readonly int widthSum;


        public MessageSplit(string message)
        {
            this.message = message;
            utf8Widths = new int[message.Length];
            for (int i = 0; i < message.Length; i++)
            {
                var width = Utf8Width(message[i]);
                utf8Widths[i] = width;
                widthSum += width;
            }
        }


        public static List<string> IrcSplit(string message, int maxByteCount)
        {
            var m = new MessageSplit(message);
            return m.GetChunks(maxByteCount);
        }

        public List<string> GetChunks(int maxByteCount)
        {
            var chunks = new List<string>();

            // Only worry about graphemes when we actually need to cut.
            if (widthSum <= maxByteCount)
                chunks.Add(message);
            else
            {
                // Index into message for current grapheme, for reading grapheme
                // byte widths and to know where a cut ends (not inclusive).
                int graphemeIdx = 0;
                // Index into message for where to start cutting.
                int substringIdx = 0;
                int byteCount = 0;
                int remaining = widthSum;

                foreach (int len in GraphemeLengths(message))
                {
                    int width = GraphemeWidth(graphemeIdx, len);
                    // If the width of the current grapheme pushes us past the limit, make the cut.
                    if (byteCount + width > maxByteCount)
                    {
                        // Cut up to, but not including, the current grapheme.
                        int count = graphemeIdx - substringIdx;
                        chunks.Add( message.Substring(substringIdx, count) );
                        // Next substring should start where we ended.
                        substringIdx = graphemeIdx;

                        // Early loop exit: remainder doesn't need to get cut.
                        remaining -= byteCount;
                        if (remaining <= maxByteCount)
                            break;
                        
                        byteCount = 0;
                    }
                    byteCount += width;
                    // Update for next loop.
                    graphemeIdx += len;
                }
                // Last substring to sweep up the remainder.
                chunks.Add( message.Substring(substringIdx, message.Length - substringIdx) );
            }

            return chunks;
        }

        int GraphemeWidth(int start, int count)
        {
            int width = 0;
            for (int i = 0; i < count; i++)
            {
                width += utf8Widths[start + i];
            }
            return width;
        }

        static int[] GraphemeLengths(string message)
        {
            // Using indices in the main (foreach) loop leads to too much clutter.
            // (Because of the look-ahead to see where the grapheme stops)
            // Since both indices and lengths are ints, let's reuse the array.
            int[] indices = StringInfo.ParseCombiningCharacters(message);

            int last = indices.Length - 1;
            for (int i = 0; i < last; i++)
            {
                // Update index to actually be length.
                indices[i] = indices[i + 1] - indices[i];
            }
            indices[last] = message.Length - indices[last];

            return indices;
        }


        // Width of a _single_ codepoint (represented in C# as `char`).
        static int Utf8Width(char c)
        {
            var codepoint = (int)c;

            if (codepoint <= 0x007F)
                return 1;
            if (codepoint <= 0x07FF)
                return 2;
            if (codepoint <= 0xFFFF)
                return 3;
            if (codepoint <= 0x10FFFF)
                return 4;

            throw new NotSupportedException("Passed character exceeds UTF-8 limits.");
        }
    }
}

