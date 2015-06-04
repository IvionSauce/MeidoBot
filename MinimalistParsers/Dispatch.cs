using System;
using System.IO;

namespace MinimalistParsers
{
    public static class Dispatch
    {
        static Func<Stream, MediaProperties>[] mediaDispatch;


        static Dispatch()
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

                if (PropSucces(props))
                    return props;
            }

            return new MediaProperties();
        }

        static bool PropSucces(MediaProperties props)
        {
            if (props.Type == MediaType.NotSupported)
                return false;
            else
                return true;
        }
    }
}