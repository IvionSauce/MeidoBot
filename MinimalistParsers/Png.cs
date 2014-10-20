using System;
using System.IO;
using System.Text;


namespace MinimalistParsers
{
    public static class Png
    {
        static readonly byte[] pngId = {0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a};
        const string pngHeader = "IHDR";


        public static MediaProperties Parse(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            if (stream.Length >= 24 && stream.ReadAndCompare(pngId))
            {
                // [Length|4] [IHDR|4] [Width|4] [Height|4]
                // Skip past IHDR length field.
                stream.Position += 4;

                var ihdr = new byte[4];
                stream.ReadInto(ihdr);

                if (Encoding.ASCII.GetString(ihdr).Equals(pngHeader, StringComparison.OrdinalIgnoreCase))
                {
                    var width = stream.ReadUint(4);
                    var height = stream.ReadUint(4);
                    var dim = new Dimensions(width, height);

                    return new MediaProperties(MediaType.Png, dim);
                }
                return new MediaProperties(MediaType.Png);
            }

            return new MediaProperties();
        }
    }
}