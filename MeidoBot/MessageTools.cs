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
                // Index into message for where to start cutting.
                int chunkStart = 0;
                // Total remaining bytes.
                int remaining = widthSum;

                while (remaining > maxByteCount)
                {
                    int bytesRead;
                    int cutIdx = SeekToCutIndex(chunkStart, maxByteCount, out bytesRead);
                    remaining -= bytesRead;

                    chunks.Add( message.Substring(chunkStart, cutIdx - chunkStart) );
                    // Next chunk should start where we ended.
                    chunkStart = cutIdx;
                }
                // Remainder is smaller or equal to maxByteCount, last chunk goes to end of string.
                chunks.Add( message.Substring(chunkStart, message.Length - chunkStart) );
            }

            return chunks;
        }

        // Move over grapheme boundaries to find a place to cut such that the resulting substring
        // takes equal to or less than `maxByteCount` bytes.
        int SeekToCutIndex(int start, int maxByteCount, out int bytesRead)
        {
            var textEnum = StringInfo.GetTextElementEnumerator(message, start);

            if (textEnum.MoveNext())
            {
                int currentIdx = textEnum.ElementIndex;
                int byteCount = 0;

                while (textEnum.MoveNext())
                {
                    int nextIdx = textEnum.ElementIndex;

                    // Length of the current grapheme (in number of chars), starting at `currentIdx`.
                    int length = nextIdx - currentIdx;
                    // Number of bytes necessary to represent grapheme in UTF-8.
                    int width = GraphemeWidth(currentIdx, length);
                    // If the current grapheme pushes us past the limit, make the cut.
                    if (byteCount + width > maxByteCount)
                    {
                        bytesRead = byteCount;
                        // Cut up to, but not including, the current grapheme.
                        return currentIdx;
                    }
                    byteCount += width;
                    currentIdx = nextIdx;
                }
            }

            // Handling cleanup of the final grapheme and cases where we can't MoveNext are annoying,
            // the caller should've already determined whether the remainder is small enough to
            // fit inside one message chunk (thus no more need to search for a place to cut).
            throw new InvalidOperationException("Caller should ensure remaining bytes are greater than max bytes.");
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


        static int Utf8Width(char c)
        {
            var codepoint = (int)c;

            // ASCII takes 1 byte.
            if (codepoint <= 0x007F)
                return 1;
            // UTF-8 can encode part of the BMP in 2 bytes...
            if (codepoint <= 0x07FF)
                return 2;
            
            // Special case: we're dealing with UTF-16 chars, everything outside the BMP (0000–​FFFF)
            // is encoded as a surrogate _pair_. Conveniently planes outside the BMP need 4 bytes in UTF-8.
            // So return 2 for each of the pair (this assumes a well-formed string).
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Surrogate)
                return 2;

            // The other part of the BMP.
            // `codepoint <= 0xFFFF` (char.MaxValue, and the last codepoint of the BMP)
            return 3;
        }
    }
}

