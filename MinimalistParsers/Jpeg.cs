using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// Many thanks to spender on StackOverflow http://stackoverflow.com/questions/4092624/ for giving me my first taste of
// how to approach a binary file programmatically, especially where the fields you want aren't in a static place.

// For a broad overview this was helpful http://www.w3.org/Graphics/JPEG/jfif3.pdf also the wikipedia page, which listed
// common markers http://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
// Neither of these gave any details about the interior organization of most segments, the following link has a few
// http://www.xbdev.net/image_formats/jpeg/tut_jpg/jpeg_file_layout.php

namespace MinimalistParsers
{
    public static class Jpeg
    {
        static readonly byte[] jpgId = new byte[] {0xff, 0xd8};

        public static ImageProperties GetProperties(Stream stream)
        {
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            long segmentStart;
            uint segmentLength;
            // General layout: [marker|2] [length|2] [data|`length-2`]
            // The marker always starts with 0xff and follows with a segment identifier.
            var marker = new byte[2];

            stream.ReadInto(marker);
            if (marker.SequenceEqual(jpgId))
            {
                stream.ReadInto(marker);
                segmentStart = stream.Position;

                // Once we've checked for the magic number (0xff 0xd8 0xff) assume we're dealing with a JPEG.
                if (marker[0] == 0xff)
                {
                    segmentLength = stream.ReadUint(2);

                    // TODO: Extra checks to ascertain it's a JPEG.
                    // 0xe0 ~ JFIF
                    if (marker[1] == 0xe0)
                    {}
                    // 0xe1 ~ Exif
                    else if (marker[1] == 0xe1)
                    {}

                    segmentStart += segmentLength;
                    while (segmentStart < stream.Length)
                    {
                        stream.Position = segmentStart;
                        stream.ReadInto(marker);
                        segmentLength = stream.ReadUint(2);

                        if (IsSof(marker) && segmentLength >= 7)
                        {
                            // Skip over the Sample Precision field.
                            stream.Position += 1;

                            var height = stream.ReadUint(2);
                            var width = stream.ReadUint(2);
                            var dim = new Dimensions(width, height);

                            return new ImageProperties(ImageType.Jpeg, dim);
                        }

                        // Our `position` is at the marker and the `segmentLength` does not include the marker, just
                        // the length of the length field and the data field.
                        segmentStart += segmentLength + 2;
                    } // while
                } // if
                return new ImageProperties(ImageType.Jpeg);
            } // if

            return new ImageProperties();
        }

        static bool IsSof(byte[] marker)
        {
            // If we find the Start of Frame marker, SOF0/SOF2 (Baseline/Progressive).
            return marker[0] == 0xff && (marker[1] == 0xc0 || marker[1] == 0xc2);
        }
    }
}