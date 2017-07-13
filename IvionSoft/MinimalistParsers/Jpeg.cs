using System;
using System.IO;

// Many thanks to spender on StackOverflow http://stackoverflow.com/questions/4092624/ for giving me my first taste of
// how to approach a binary file programmatically, especially where the fields you want aren't in a static place.

// For a broad overview this was helpful http://www.w3.org/Graphics/JPEG/jfif3.pdf also the wikipedia page, which listed
// common markers http://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
// Neither of these gave any details about the interior organization of most segments, the following link has a few
// http://www.xbdev.net/image_formats/jpeg/tut_jpg/jpeg_file_layout.php

namespace IvionSoft.MinimalistParsers
{
    public static class Jpeg
    {
        static readonly byte[] jpgId = new byte[] {0xff, 0xd8, 0xff};


        public static MediaProperties Parse(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            // General layout: [marker|2] [length|2] [data|`length-2`]
            // The marker always starts with 0xff and follows with a segment identifier.
            var marker = new byte[2];
            uint segmentLength;

            if (IsJpeg(stream))
            {
                // Search for the Start of Frame marker.
                while (stream.Position < stream.Length)
                {
                    stream.ReadInto(marker);
                    segmentLength = stream.ReadUint(2);

                    // Test for SOF and if the stream is long enough for the values that we want (width/height).
                    if (IsSof(marker) && (stream.Position + 5) <= stream.Length)
                    {
                        // Skip over the Sample Precision field.
                        stream.Position += 1;
                        
                        var height = stream.ReadUint(2);
                        var width = stream.ReadUint(2);
                        var dim = new Dimensions(width, height);
                        
                        return new MediaProperties(MediaType.Jpeg, dim);
                    }

                    // Protect against going backwards due to bogus segment lengths.
                    // Also unsigned integers will fuck everything up when they wrap around. Which will happen
                    // without this check.
                    if (segmentLength > 2)
                        stream.Position += segmentLength - 2;
                } // while
                return new MediaProperties(MediaType.Jpeg);
            } // if
            return new MediaProperties();
        }


        static bool IsJpeg(Stream stream)
        {
            if (stream.ReadAndCompare(jpgId))
            {
                // The first segment in a Jpeg will be an APPn segment.
                // Except when it's not...
                int segmentId = stream.ReadByte();
                // 0xe0 ~ JFIF (APP0)
                // 0xe1 ~ Exif (APP1)
                // 0xe2 ~ ICC Profile (APP2)
                // [...]
                // Non-APPn segments:
                // 0xdb ~ Quantization Tables (DQT)
                // 0xfe ~ Comment (COM)
                if ( (segmentId >= 0xe0 && segmentId < 0xf0) || segmentId == 0xdb || segmentId == 0xfe )
                {
                    var segmentLength = stream.ReadUint(2);
                    stream.Position += segmentLength - 2;
                    return true;
                }
            }
            return false;
        }

        static bool IsSof(byte[] marker)
        {
            // If we find the Start of Frame marker, SOF0/SOF2 (Baseline/Progressive).
            return marker[0] == 0xff && (marker[1] == 0xc0 || marker[1] == 0xc2);
        }
    }
}