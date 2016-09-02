using System;
using System.IO;

namespace IvionSoft.MinimalistParsers
{
    public static class Gif
    {
        // GIF
        static readonly byte[] gifId = {0x47, 0x49, 0x46};
        // Version 89a
        static readonly byte[] newId = {0x38, 0x39, 0x61};
        // Version 87a
        static readonly byte[] oldId = {0x38, 0x37, 0x61};


        public static MediaProperties Parse(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            if (stream.Length >= 10 && IsGif(stream))
            {
                var bDims = new byte[4];
                stream.ReadInto(bDims);
                // GIF is little endian, least significant byte first.
                int width = bDims[0] + (bDims[1] << 8);
                int height = bDims[2] + (bDims[3] << 8);

                return new MediaProperties(MediaType.Gif, new Dimensions(width, height));
            }
            return new MediaProperties();
        }


        static bool IsGif(Stream stream)
        {
            if (stream.ReadAndCompare(gifId))
            {
                var version = new byte[3];
                stream.ReadInto(version);
                if (version.StartsWith(newId) || version.StartsWith(oldId))
                    return true;
            }
            return false;
        }
    }
}