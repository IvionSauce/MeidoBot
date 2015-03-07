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


        public static TitlingResult BinaryToIrc(TitlingRequest req)
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
            
            FormatBinaryInfo(req.ConstructedTitle, type, media);
            return req.CreateResult(true);
        }

        static void FormatBinaryInfo(TitleConstruct title, string content, MediaInfo media)
        {
            if (media.Dimensions.Width > 0 && media.Dimensions.Height > 0)
            {
                title.SetFormat("[ {0}: {1}x{2} ]", content, media.Dimensions.Width, media.Dimensions.Height);
                title.AppendTime(media.Duration);

                if (media.HasAudio)
                    title.Append('♫');

                title.AppendSize(media.ContentLength);
            }
            else
                title.SetFormat("[ {0} ]", content).AppendSize(media.ContentLength);
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