using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

// Thanks to the EBML spec http://www.matroska.org/technical/specs/index.html and the EBML RFC draft
// http://www.matroska.org/technical/specs/rfc/index.html especially section 2 of the RFC draft, which gives a good
// overview of the various types you can encounter when parsing EBML. The most common type is the variable size integer,
// used for both the Element ID and the Size of elements, and is also the most unusual.

// I want to give notice to the libebml parser (part of the Matroska project) whose implementation of a variable size
// integer reader helped me immensely in understanding and implementing my own.
// https://github.com/Matroska-Org/libebml/blob/master/src/EbmlElement.cpp the function is called ReadCodedSizeValue.

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
         * L3 Audio         E1              0x61
         * 
         * L1 Info          15-49-A9-66     0x0549A966
         * L2 TimecodeScale 2A-D7-B1        0x0AD7B1
         * L2 Duration      44-89           0x0489
         */
        static readonly byte[] ebmlId = {0x1a, 0x45, 0xdf, 0xa3};
        const long DoctypeId = 0x282;

        const long SegmentId = 0x08538067;
        const long TracksId = 0x0654AE6B;
        const long TrackEntryId = 0x2E;

        static readonly long[] videoAndAudioIds = {0x60, 0x61};
        static readonly long[] dimensionIds = {0x30, 0x3A};
        
        const long InfoId = 0x0549A966;
        static readonly long[] durationIds = {0x0AD7B1, 0x0489};
        
        
        public static MediaProperties Parse(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            if ( stream.ReadAndCompare(ebmlId) )
            {
                long len = ReadVarInt(stream);
                long ebmlBlockEnd = stream.Position + len;
                
                var bDoc = Get(stream, DoctypeId, ebmlBlockEnd) ?? new byte[0];
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

                // Both Info and Tracks are contained in the Segment.
                if ( Match(stream, SegmentId, out len) )
                {
                    var time = GetDuration(stream);
                    var trackinfo = GetTrackInfo(stream);
                    return new MediaProperties(type, trackinfo.Item1, time, trackinfo.Item2);
                }

                return new MediaProperties(type);
            }
            return new MediaProperties();
        }
        
        static TimeSpan GetDuration(Stream stream)
        {
            // Timescale*Duration gives us the time in nanoseconds, divide by this to get milliseconds.
            const int milliSecScale = 1000000;

            long len;
            if ( Match(stream, InfoId, out len) )
            {
                byte[][] bDur = Get(stream, durationIds, stream.Position + len);
                if (bDur[0] != null && bDur[1] != null)
                {
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
            }
            return TimeSpan.Zero;
        }


        static Tuple<Dimensions, bool> GetTrackInfo(Stream stream)
        {
            bool hasAudio = false;
            Dimensions dimensions = new Dimensions();

            long len;
            if ( Match(stream, TracksId, out len) )
            {
                var trackEntries = BreadthMatch(stream, TrackEntryId, stream.Position + len);

                foreach (var match in trackEntries)
                {
                    stream.Position = match.Start;

                    var tracks = MultiMatch(stream, videoAndAudioIds, match.Stop);
                    // Video
                    if ( tracks[0] != null )
                    {
                        stream.Position = tracks[0].Start;
                        byte[][] bDims = Get(stream, dimensionIds, tracks[0].Stop);

                        if (bDims[0] != null && bDims[1] != null)
                            dimensions = new Dimensions(bDims[0].ToUlong(), bDims[1].ToUlong());
                    }
                    // Audio
                    if ( tracks[1] != null )
                        hasAudio = true;

                } // foreach
            } // if

            return new Tuple<Dimensions, bool>(dimensions, hasAudio);
        }


        // -----
        // Above: combining methods to extract the information I want.
        // Below: general methods for selectively parsing EBML.
        // -----


        static byte[] Get(Stream stream, long id, long readTo)
        {
            var results = Get(stream, new long[] {id}, readTo);
            return results[0];
        }
        
        static byte[][] Get(Stream stream, long[] ids, long readTo)
        {
            var matches = MultiMatch(stream, ids, readTo);
            var results = new byte[ids.Length][];
            for (int i = 0; i < matches.Length; i++)
            {
                var match = matches[i];
                if (match != null)
                {
                    stream.Position = match.Start;
                    var data = new byte[match.Length];
                    stream.ReadInto(data);

                    results[i] = data;
                }
            }
            return results;
        }


        static StreamChunk[] MultiMatch(Stream stream, long[] idMatches, long readTo)
        {
            var matches = new StreamChunk[idMatches.Length];
            int matchCount = 0;

            while (stream.Position < readTo)
            {
                long id = ReadVarId(stream);
                long len = ReadVarInt(stream);
                if (len < 0)
                    break;

                int matchIndex = Array.IndexOf(idMatches, id);
                if (matchIndex >= 0)
                {
                    matches[matchIndex] = new StreamChunk(stream.Position, len);
                    matchCount++;
                    if (matchCount == idMatches.Length)
                        break;
                }
                stream.Position += len;
            }
            return matches;
        }


        static List<StreamChunk> BreadthMatch(Stream stream, long idMatch, long readTo)
        {
            var matches = new List<StreamChunk>();
            long len;
            while ( Match(stream, idMatch, readTo, out len) )
            {
                matches.Add( new StreamChunk(stream.Position, len) );
                stream.Position += len;
            }
            
            return matches;
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
                    break;
                else
                    stream.Position += len;
            }

            return false;
        }
        
        
        static long ReadVarId(Stream stream)
        {
            // The maximum ID Length is 4 bytes... according to the specs the max would be 2^28-1.
            const long MaxId = 268435455;
            
            long varint = ReadVarInt(stream);
            if (varint <= MaxId)
                return varint;
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