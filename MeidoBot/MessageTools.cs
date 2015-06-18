using System;
using System.Text;
using System.Collections.Generic;


namespace MeidoBot
{
    static class MessageTools
    {
        // It should be noted that the 512 character limit for IRC messages means 512 bytes/octets. This is why we
        // need to look up how much bytes a certain character takes in UTF-8 (the encoding we use to send the messages).

        public static List<string> Split(string message, int maxBytes)
        {
            var splitMessages = new List<string>();

            int start = 0;
            while (start < message.Length)
            {
                var shortMsg = Shorten(message, start, maxBytes);
                splitMessages.Add(shortMsg);
                start += shortMsg.Length;
            }

            return splitMessages;
        }


        public static string Shorten(string message, int start, int maxBytes)
        {
            var tmpMsg = new StringBuilder();

            int byteCount = 0;
            for (int i = start; i < message.Length; i++)
            {
                char currentChar = message[i];
                int width = Utf8Width(currentChar);

                if ( (byteCount + width) <= maxBytes)
                {
                    tmpMsg.Append(currentChar);
                    byteCount += width;
                }
                else
                    break;
            }

            return tmpMsg.ToString();
        }


        static int Utf8Width(char c)
        {
            int codepoint = (int)c;

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

