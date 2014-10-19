using System;
using System.IO;

namespace MinimalistParsers
{
    internal class StreamChunk
    {
        public long Start { get; private set; }
        public long Length { get; private set; }
        public long Stop { get; private set; }


        public StreamChunk(long start, long length)
        {
            Start = start;
            Length = length;
            Stop = start + length;
        }


        public static implicit operator bool(StreamChunk c)
        {
            return c.Length >= 0;
        }
    }
}