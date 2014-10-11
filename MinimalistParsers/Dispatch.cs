using System;
using System.IO;
using System.Collections.Generic;

namespace MinimalistParsers
{
    public static class Dispatch
    {
        static Func<Stream, MediaProperties>[] mediaDispatch;


        static Dispatch()
        {
            mediaDispatch = new Func<Stream, MediaProperties>[]
            {
                Png.GetProperties,
                Jpeg.GetProperties,
                Ebml.Parse
            };
        }


        public static MediaProperties GetMediaInfo(Stream stream)
        {
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