using System;
using IvionWebSoft;
using MinimalistParsers;


namespace WebIrc
{
    public static class MiscHandlers
    {
        public static TitlingResult YoutubeWithDuration(TitlingRequest req, string htmlDoc)
        {
            // If duration can be found, change the html info to include that.
            var ytTime = WebTools.GetYoutubeTime(htmlDoc);
            req.ConstructedTitle.SetHtmlTitle().AppendTime(ytTime);

            return req.CreateResult(true);
        }


        public static TitlingResult BinaryToIrc(TitlingRequest req, WebBytes wb)
        {
            req.Resource = wb;
            if (!wb.Success)
                return req.CreateResult(false);

            var media = MediaDispatch.Parse(wb.Data);

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
                type = wb.ContentType;
                req.AddMessage("Binary format not supported.");
                break;
            }

            FormatBinaryInfo(req.ConstructedTitle, type, media, wb.ContentLength);
            return req.CreateResult(true);
        }

        static void FormatBinaryInfo(TitleConstruct title, string content, MediaProperties media, long length)
        {
            if (media.Dimensions.Width > 0 && media.Dimensions.Height > 0)
            {
                title.SetFormat("[ {0}: {1}x{2} ]", content, media.Dimensions.Width, media.Dimensions.Height);
                title.AppendTime(media.Duration);

                if (media.HasAudio)
                    title.Append('♫');

                title.AppendSize(length);
            }
            else
                title.SetFormat("[ {0} ]", content).AppendSize(length);
        }
    }
}