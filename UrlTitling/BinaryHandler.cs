using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using IvionWebSoft;
using MinimalistParsers;


namespace WebIrc
{
    internal static class BinaryHandler
    {
        public static RequestResult MediaToIrc(RequestObject req)
        {
            var info = GetInfo(req.Url);
            if (info == null)
                return req.CreateResult(false);
            req.Resource = info;

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
                req.AddMessage("Binary format not supported.");
                return req.CreateResult(false);
            }

            if (info.Dimensions.Width > 0 && info.Dimensions.Height > 0)
            {
                req.ConstructedTitle = string.Format("[ {0}: {1}x{2} ] {3}", type,
                                                     info.Dimensions.Width, info.Dimensions.Height,
                                                     FormatSize(info.Size));
                return req.CreateResult(true);
            }
            else
            {
                req.AddMessage( string.Format("Failed to parse {0} dimensions.", type) );
                return req.CreateResult(false);
            }
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


        public static ImageInfo GetInfo(string url)
        {
            try
            {
                var uri = new Uri(url);
                return InternalGet(uri);
            }
            catch (Exception ex)
            {
                if (ex is WebException || ex is UriFormatException)
                    return new ImageInfo(ex);
                // Debug
                else
                {
                    Console.WriteLine(ex.ToString());
                    return null;
                }
            }
        }

        static ImageInfo InternalGet(Uri url)
        {
            const int FetchSize = 65536;
            
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 30000;
            req.Accept = "*/*";

            long contentSize;
            using (var ms = new MemoryStream())
            {
                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                {
                    contentSize = response.ContentLength;
                    ReadFragment(response.GetResponseStream(), ms, FetchSize);
                }

                ImageProperties props = Dispatch.GetImageInfo(ms);
                return new ImageInfo(url, props, contentSize);
            }
        }

        // Read a fragment of the stream into a memorystream.
        static void ReadFragment(Stream stream, MemoryStream ms, int fragmentSize)
        {            
            var buffer = new byte[fragmentSize];
            
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (ms.Length >= fragmentSize || read <= 0)
                {
                    ms.Flush();
                    ms.Position = 0;
                    return;
                }
                ms.Write(buffer, 0, read);
            }
        }
    }


    public class ImageInfo : WebResource
    {
        public ImageType Type { get; private set; }
        public Dimensions Dimensions { get; private set; }
        public long Size { get; private set; }


        public ImageInfo(Exception ex) : base(ex) {}

        public ImageInfo(Uri uri, Exception ex) : base(uri, ex) {}

        public ImageInfo(Uri uri, ImageProperties props) : this(uri, props, 0) {}

        public ImageInfo(Uri uri, ImageProperties props, long size) : base(uri)
        {
            Type = props.Type;
            Dimensions = props.Dimensions;
            Size = size;
        }
    }
}