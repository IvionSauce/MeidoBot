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
            MediaInfo media;
            using (BinaryPeek peek = MinimalWeb.Peek(req.Uri, FetchSize))
            {
                media = GetInfo(peek);
            }
            req.Resource = media;
            if (!media.Success)
                return req.CreateResult(false);
                
            string type;
            switch (media.Type)
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
                type = media.ContentType;
                req.AddMessage("Binary format not supported.");
                break;
            }
            
            req.ConstructedTitle = FormatBinaryInfo(type, media);
            return req.CreateResult(true);
        }

        static string FormatBinaryInfo(string content, MediaInfo media)
        {
            var sizeStr = FormatSize(media.ContentLength);

            string binaryInfo;
            if (media.Dimensions.Width > 0 && media.Dimensions.Height > 0)
            {
                var timeStr = FormatTime(media.Duration);
                if (timeStr != string.Empty && media.HasAudio)
                    timeStr += " ♫";

                binaryInfo = string.Format("[ {0}: {1}x{2} ]{3} {4}",
                                           content, media.Dimensions.Width, media.Dimensions.Height,
                                           timeStr, sizeStr);
            }
            else
                binaryInfo = string.Format("[ {0} ] {1}", content, sizeStr);

            return binaryInfo;
        }

        // Size is in bytes/octets.
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
            if (duration.TotalSeconds < 1d)
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
                return new MediaInfo(peek, props);
            }
            else
                return new MediaInfo(peek);
        }
    }


    class MediaInfo : WebResource
    {
        public MediaType Type { get; private set; }
        public Dimensions Dimensions { get; private set; }
        public TimeSpan Duration { get; private set; }
        public bool HasAudio { get; private set; }

        public string ContentType { get; private set; }
        public long ContentLength { get; private set; }


        public MediaInfo(WebResource resource) : base(resource) {}

        public MediaInfo(BinaryPeek peek, MediaProperties props) : base(peek.Location)
        {
            Type = props.Type;
            Dimensions = props.Dimensions;
            Duration = props.Duration;
            HasAudio = props.HasAudio;

            ContentType = peek.ContentType;
            ContentLength = peek.ContentLength;
        }
    }

}