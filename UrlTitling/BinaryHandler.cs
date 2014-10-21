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
            using (BinaryPeek peek = MinimalWeb.Peek(req.Uri, FetchSize))
            {
                var info = GetInfo(peek);
                req.Resource = info;
                if (!info.Success)
                    return req.CreateResult(false);
                
                string type;
                switch (info.Type)
                {
                case MediaType.Jpeg:
                    type = "JPEG";
                    break;
                case MediaType.Png:
                    type = "PNG";
                    break;
                case MediaType.Gif:
                    type = "GIF";
                    break;
                case MediaType.Matroska:
                    type = "Matroska";
                    break;
                case MediaType.Webm:
                    type = "WebM";
                    break;
                default:
                    type = peek.ContentType;
                    req.AddMessage("Binary format not supported.");
                    break;
                }
                
                req.ConstructedTitle = FormatBinaryInfo(type, info);
            }

            return req.CreateResult(true);
        }

        static string FormatBinaryInfo(string content, MediaInfo info)
        {
            var sizeStr = FormatSize(info.Size);

            string binaryInfo;
            if (info.Dimensions.Width > 0 && info.Dimensions.Height > 0)
            {
                var timeStr = FormatTime(info.Duration);
                if (timeStr != string.Empty && info.HasAudio)
                    timeStr += " ♫";

                binaryInfo = string.Format("[ {0}: {1}x{2} ]{3} {4}",
                                           content, info.Dimensions.Width, info.Dimensions.Height,
                                           timeStr, sizeStr);
            }
            else
                binaryInfo = string.Format("[ {0} ] {1}", content, sizeStr);

            return binaryInfo;
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

        static string FormatTime(TimeSpan duration)
        {
            if (duration.TotalSeconds <= 0)
                return string.Empty;

            var total = (int)Math.Round(duration.TotalSeconds);
            var minutes = total / 60;
            var seconds = total % 60;

            return string.Format(" [{0}:{1:00}]", minutes, seconds);
        }


        static MediaInfo GetInfo(BinaryPeek peek)
        {
            if (peek.Success)
            {
                MediaProperties props = Dispatch.GetMediaInfo(peek.Peek);
                return new MediaInfo(peek.Location, props, peek.ContentLength);
            }
            else
                return new MediaInfo(peek);
        }
    }


    public class MediaInfo : WebResource
    {
        public MediaType Type { get; private set; }
        public Dimensions Dimensions { get; private set; }
        public TimeSpan Duration { get; private set; }
        public bool HasAudio { get; private set; }
        public long Size { get; private set; }


        public MediaInfo(WebResource resource) : base(resource) {}

        public MediaInfo(Uri uri, MediaProperties props) : this(uri, props, 0) {}

        public MediaInfo(Uri uri, MediaProperties props, long size) : base(uri)
        {
            Type = props.Type;
            Dimensions = props.Dimensions;
            Duration = props.Duration;
            HasAudio = props.HasAudio;
            Size = size;
        }
    }

}