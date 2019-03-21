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

    // Besides that, Unicode has graphemes: which are 1 visual character, but are made out of multiple codepoints.
    // We want to cut at grapheme boundaries, so we'll iterate graphemes instead of chars.

    class MessageSplit
    {
        readonly string message;
        readonly int[] utf8Widths;
        readonly int widthSum;


        public MessageSplit(string message)
        {
            this.message = message;
            utf8Widths = new int[message.Length];

            int index = 0;
            while (index < message.Length)
            {
                // DoCodepoint monkeys with the index, depending on whether we
                // have a normal UTF-16 char or a surrogate pair.
                widthSum += DoCodepoint(ref index);
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
                CutMessageInto(chunks, maxByteCount);

            return chunks;
        }

        void CutMessageInto(List<string> chunks, int maxByteCount)
        {
            // Index into message for where to start cutting.
            int chunkStart = 0;
            // Total remaining bytes.
            int remaining = widthSum;

            while (remaining > maxByteCount)
            {
                int bytesRead;
                int cutIdx = SeekToCutIndex(chunkStart, maxByteCount, out bytesRead);
                if (cutIdx > chunkStart)
                {
                    chunks.Add( message.Substring(chunkStart, cutIdx - chunkStart) );
                    // Next chunk should start where we ended.
                    chunkStart = cutIdx;
                }
                // Oh no, we're stuck - this is due to a grapheme not fitting inside
                // `maxByteCount`. So we skip it.
                else
                {
                    chunkStart = SkipGrapheme(chunkStart, out bytesRead);
                    // If this skip puts us at the end of string return immediately,
                    // there is no remainder.
                    if (chunkStart == message.Length)
                        return;
                }
                remaining -= bytesRead;
            }
            // Remainder is smaller or equal to maxByteCount, last chunk goes to end of string.
            chunks.Add( message.Substring(chunkStart, message.Length - chunkStart) );
        }


        // Move over grapheme boundaries to find a place to cut such that the resulting substring
        // takes equal to or less than `maxByteCount` bytes.
        int SeekToCutIndex(int start, int maxByteCount, out int bytesRead)
        {
            var textEnum = StringInfo.GetTextElementEnumerator(message, start);
            if (!textEnum.MoveNext())
                throw new InvalidOperationException("Starting index was end of string.");
            
            int currentIdx = textEnum.ElementIndex;
            int byteCount = 0;
            bool seeking = true;

            while (seeking)
            {
                int nextIdx;
                if (textEnum.MoveNext())
                    nextIdx = textEnum.ElementIndex;
                else
                {
                    // Handle last grapheme.
                    nextIdx = message.Length;
                    seeking = false;
                }
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

            // We've reached the end of string and the remaining grapheme(s)
            // are smaller than `maxByteCount`.
            bytesRead = byteCount;
            return message.Length;
        }

        // Method for skipping over pesky, overly long graphemes (longer than max bytes).
        // This should be extemely rare.
        int SkipGrapheme(int start, out int bytesSkipped)
        {
            var textEnum = StringInfo.GetTextElementEnumerator(message, start);
            if (!textEnum.MoveNext())
                throw new InvalidOperationException("Starting index was end of string.");

            int currentIdx = textEnum.ElementIndex;
            int nextIdx;
            if (textEnum.MoveNext())
                nextIdx = textEnum.ElementIndex;
            else
                nextIdx = message.Length;

            bytesSkipped = GraphemeWidth(start, nextIdx - currentIdx);
            return nextIdx;
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


        // A codepoint can be 1 or 2 UTF-16 chars.
        // Returns byte width of the codepoint when encoded in UTF-8.
        int DoCodepoint(ref int index)
        {
            char highSurrogate = message[index];
            if (highSurrogate >= 0xD800 &&
                highSurrogate <= 0xDBFF &&
                index + 1 < message.Length)
            {
                char lowSurrogate = message[index + 1];
                // Valid surrogate pair.
                if (lowSurrogate >= 0xDC00 && lowSurrogate <= 0xDFFF)
                {
                    // We're dealing with UTF-16 chars, everything outside the BMP (0000–​FFFF) is encoded
                    // as a surrogate _pair_. Conveniently planes outside the BMP need 4 bytes in UTF-8.
                    // So set byte width to 2 for each of the pair. This is a futzy hack.
                    utf8Widths[index] = 2;
                    utf8Widths[index + 1] = 2;

                    index += 2;
                    return 4;
                }
            }

            // For regular UTF-16 chars and surrogates without their partner this will
            // set the right width.
            utf8Widths[index] = Utf8Width(highSurrogate);
            index++;
            return utf8Widths[index];
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
            // The other part of the BMP.
            // `codepoint <= 0xFFFF` (char.MaxValue, and the last codepoint of the BMP)
            return 3;
        }
    }
}

