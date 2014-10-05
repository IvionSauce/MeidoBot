using System;
using System.IO;
using System.Collections.Generic;

namespace MinimalistParsers
{
    public static class Dispatch
    {
        static Func<Stream, ImageProperties>[] imgDispatch;


        static Dispatch()
        {
            imgDispatch = new Func<Stream, ImageProperties>[]
            {
                Png.GetProperties,
                Jpeg.GetProperties
            };
        }


        public static ImageProperties GetImageInfo(Stream stream)
        {
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.");

            foreach (var f in imgDispatch)
            {
                stream.Position = 0;
                ImageProperties props = f(stream);

                if (PropSucces(props))
                    return props;
            }

            return new ImageProperties();
        }

        static bool PropSucces(ImageProperties props)
        {
            if (props.Dimensions.Width > 0 && props.Dimensions.Height > 0)
                return true;
            else
                return false;
        }
    }
}