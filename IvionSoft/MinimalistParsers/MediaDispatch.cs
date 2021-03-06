using System;
using System.IO;

namespace IvionSoft.MinimalistParsers
{
    public static class MediaDispatch
    {
        static Func<Stream, MediaProperties>[] mediaDispatch;


        static MediaDispatch()
        {
            mediaDispatch = new Func<Stream, MediaProperties>[]
            {
                Png.Parse,
                Gif.Parse,
                Jpeg.Parse,
                Ebml.Parse
            };
        }


        public static MediaProperties Parse(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            return Parse(new MemoryStream(data));
        }

        public static MediaProperties Parse(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            foreach (var f in mediaDispatch)
            {
                stream.Position = 0;
                MediaProperties props = f(stream);

                if (PropSuccess(props))
                    return props;
            }

            return new MediaProperties();
        }

        static bool PropSuccess(MediaProperties props)
        {
            if (props.Type == MediaType.NotSupported)
                return false;
            else
                return true;
        }
    }
}