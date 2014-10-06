using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using IvionWebSoft;
using MinimalistParsers;


namespace WebIrc
{
    public static class BinaryHandler
    {
        const int FetchSize = 65536;


        public static RequestResult BinaryToIrc(RequestObject req)
        {
            BinaryPeek peek = MinimalWeb.Peek(req.Uri, FetchSize);
            var info = GetInfo(peek);
            req.Resource = info;

            if (!info.Success)
                return req.CreateResult(false);

            string type;
            switch (info.Type)
            {
            case ImageType.Jpeg:
                type = "JPEG";
                break;
            case ImageType.Png:
                type = "PNG";
                break;
            default:
                type = peek.ContentType;
                req.AddMessage("Binary format not supported.");
                break;
            }

            req.ConstructedTitle = FormatBinaryInfo(type, info.Dimensions, peek.ContentLength);
            return req.CreateResult(true);
        }

        static string FormatBinaryInfo(string content, Dimensions dimensions, long size)
        {
            var sizeStr = FormatSize(size);
            if (dimensions.Width > 0 && dimensions.Height > 0)
            {
                return string.Format("[ {0}: {1}x{2} ] {3}", content, dimensions.Width, dimensions.Height, sizeStr);
            }
            else
                return string.Format("[ {0} ] {1}", content, sizeStr);
        }

        // Size is in bytes.
        static string FormatSize(long size)
        {
            if (size < 1)
                return string.Empty;

            var sizeInK = size / 1024d;
            if (sizeInK > 1024)
            {
                var sizeInM = sizeInK / 1024;
                return sizeInM.ToString("#.#") + "MB";
            }
            else
                return sizeInK.ToString("#.#") + "kB";
        }


        static ImageInfo GetInfo(BinaryPeek peek)
        {
            if (peek.Success)
            {
                ImageProperties props = Dispatch.GetImageInfo(peek.Peek);
                return new ImageInfo(peek.Location, props, peek.ContentLength);
            }
            else
                return new ImageInfo(peek);
        }
    }


    public class ImageInfo : WebResource
    {
        public ImageType Type { get; private set; }
        public Dimensions Dimensions { get; private set; }
        public long Size { get; private set; }


        public ImageInfo(WebResource resource) : base(resource) {}

        public ImageInfo(Uri uri, ImageProperties props) : this(uri, props, 0) {}

        public ImageInfo(Uri uri, ImageProperties props, long size) : base(uri)
        {
            Type = props.Type;
            Dimensions = props.Dimensions;
            Size = size;
        }
    }

}