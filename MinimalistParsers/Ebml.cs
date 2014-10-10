using System;
using System.IO;
using System.Text;


namespace MinimalistParsers
{
    public static class Ebml
    {
        /* Level+Name       Stream octets   Actual number they encode for
         * 
         * L0 EBML          1A-45-DF-A3     0xA45DFA3
         * ---
         * L1 DocType       42-82           0x282
         * 
         * L0 Segment       18-53-80-67     0x08538067
         * ---
         * L1 Tracks        16-45-AE-6B     0x0654AE6B
         * L2 TrackEntry    AE              0x2E
         * L3 Video         E0              0x60
         * L4 PixelWidth    B0              0x30
         * L4 PixelHeight   BA              0x3A
         * 
         * L1 Info          15-49-A9-66     0x0549A966
         * L2 TimecodeScale 2A-D7-B1        0x0AD7B1
         * L2 Duration      44-89           0x0489
         */
        static readonly byte[] ebmlId = {0x1a, 0x45, 0xdf, 0xa3};

        const long Doctype = 0x282;
        const long SegmentId = 0x08538067;
        
        static readonly long[] videoTrack = {SegmentId, 0x0654AE6B, 0x2E, 0x60};
        static readonly long[] dims = {0x30, 0x3A};
        
        static readonly long[] info = {SegmentId, 0x0549A966};
        static readonly long[] duration = {0x0AD7B1, 0x0489};
        
        
        public static MediaProperties Parse(Stream stream)
        {
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            if ( stream.ReadAndCompare(ebmlId) )
            {
                var len = ReadVarInt(stream);
                long ebmlBlockEnd = stream.Position + len;
                
                var bDoc = Get(stream, Doctype, ebmlBlockEnd) ?? new byte[0];
                MediaType type;
                switch (Encoding.ASCII.GetString(bDoc))
                {
                case "matroska":
                    type = MediaType.Matroska;
                    break;
                case "webm":
                    type = MediaType.Webm;
                    break;
                default:
                    return new MediaProperties();
                }

                stream.Position = ebmlBlockEnd;
                
                var time = GetDuration(stream);
                var dims = GetDimensions(stream);
                return new MediaProperties(type, dims, time);
            }
            return new MediaProperties();
        }
        
        public static TimeSpan GetDuration(Stream stream)
        {
            // Timescale*Duration gives us the time in nanoseconds, divide by this to get milliseconds.
            const int milliSecScale = 1000000;
            
            var bDur = TraverseGet(stream, info, duration);
            if (bDur[0] == null || bDur[1] == null)
                return TimeSpan.Zero;
            
            var scale = bDur[0].ToUlong();
            var ticks = bDur[1].ToDouble();
            
            // Duration/ticks is already in milliseconds.
            if (scale == milliSecScale)
                return TimeSpan.FromMilliseconds(ticks);
            else
            {
                double millisecs = (scale * ticks) / milliSecScale;
                return TimeSpan.FromMilliseconds(millisecs);
            }
        }
        
        public static Dimensions GetDimensions(Stream stream)
        {
            var bDims = TraverseGet(stream, videoTrack, dims);
            if (bDims[0] == null || bDims[1] == null)
                return new Dimensions();
            
            var width = bDims[0].ToUlong();
            var height = bDims[1].ToUlong();
            return new Dimensions(width, height);
        }
        
        
        static byte[][] TraverseGet(Stream stream, long[] idsDepth, long[] getIds)
        {
            var startPos = stream.Position;
            
            byte[][] values;
            long len;
            if ( DepthMatch(stream, idsDepth, out len) )
            {
                long upperLimit = stream.Position + len;
                values = Get(stream, getIds, upperLimit);
            }
            else
                values = new byte[getIds.Length][];
            
            stream.Position = startPos;
            return values;
        }
        
        
        static bool DepthMatch(Stream stream, long[] idMatches, out long len)
        {
            len = 0;
            long upperLimit = stream.Length;
            foreach (long match in idMatches)
            {
                if ( Match(stream, match, upperLimit, out len) )
                {
                    upperLimit = stream.Position + len;
                    continue;
                }
                else
                    return false;
            }
            return true;
        }
        
        
        static byte[] Get(Stream stream, long id, long readTo)
        {
            var results = Get(stream, new long[] {id}, readTo);
            return results[0];
        }
        
        static byte[][] Get(Stream stream, long[] ids, long readTo)
        {
            var values = new byte[ids.Length][];
            var start = stream.Position;
            
            for (int i = 0; i < ids.Length; i++)
            {
                stream.Position = start;
                long len;
                if ( Match(stream, ids[i], readTo, out len) )
                {
                    var data = new byte[len];
                    stream.ReadInto(data);
                    values[i] = data;
                }
            }
            return values;
        }
        
        
        static bool Match(Stream stream, long idMatch, out long len)
        {
            return Match(stream, idMatch, stream.Length, out len);
        }
        
        static bool Match(Stream stream, long idMatch, long readTo, out long len)
        {
            len = 0;
            while (stream.Position < readTo)
            {
                long id = ReadVarId(stream);
                len = ReadVarInt(stream);
                
                if (id == idMatch)
                    return true;
                else if (len < 0)
                    return false;
                else
                    stream.Position += len;
            }
            return false;
        }
        
        
        static long ReadVarId(Stream stream)
        {
            // The maximum ID Length is 4 bytes... according to the specs the max would be 2^28-1.
            const uint MaxId = 268435455;
            
            long varint = ReadVarInt(stream);
            if (varint <= MaxId)
                return (long)varint;
            else
                return 0;
        }
        
        
        static long ReadVarInt(Stream stream)
        {
            // Starts off as 0b10000000.
            byte mask = 1 << 7;
            byte[] storage = new byte[8];
            
            for (int i = 0; i < storage.Length; i++)
            {
                // If end of the stream (ReadByte returns -1), return -1 signaling end-of-stream.
                var singleB = stream.ReadByte();
                if (singleB >= 0)
                    storage[i] = (byte)singleB;
                else
                    return -1;
                
                // If after bit-AND the byte is larger than 0 it means that the length-bit was set at place `i` in the
                // first byte.
                if ( (storage[0] & (mask >> i)) > 0 )
                {
                    // Shift mask the appropriate amount of bits. Ex at i=2: 0b00100000.
                    mask >>= i;
                    int num_bytes = i + 1;
                    
                    // Remove the length-indicator bit.
                    long varInt = storage[0] & ~mask;
                    for (int j = 1; j < num_bytes; j++)
                    {
                        varInt <<= 8;
                        varInt |= storage[j];
                    }
                    return varInt;
                } // if
            } // for
            throw new InvalidOperationException("Error parsing EBML variable int.");
        }
        
        
        internal static ulong ToUlong(this byte[] bytes)
        {
            ulong num = 0;
            foreach (byte b in bytes)
            {
                num <<= 8;
                num |= b;
            }
            return num;
        }
        
        internal static double ToDouble(this byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            
            switch (bytes.Length)
            {
            case 4:
                return BitConverter.ToSingle(bytes, 0);
            case 8:
                return BitConverter.ToDouble(bytes, 0);
            case 10:
                // Don't know how to handle a 10 byte float.
            default:
                return 0;
            }
        }
        
    }
}