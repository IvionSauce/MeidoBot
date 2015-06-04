using System;

namespace IvionWebSoft
{
    public class DanboPost : BooruPost
    {
        public string[] CopyrightTags { get; private set; }
        public string[] CharacterTags { get; private set; }
        public string[] ArtistTags { get; private set; }
        public string[] GeneralTags { get; private set; }


        public DanboPost(Uri uri, Exception ex) : base(uri, ex) {}

        public DanboPost(Uri uri,
            int postNo,
            string copyrights,
            string characters,
            string artists,
            string general,
            string all,
            string rated) : base(uri, postNo, all, rated)
        {
            CopyrightTags = Split(copyrights);
            CharacterTags = Split(characters);
            ArtistTags = Split(artists);
            GeneralTags = Split(general);
        }
    }


    public class BooruPost : WebResource
    {
        public enum Rating
        {
            Unknown,
            Safe,
            Questionable,
            Explicit
        }

        public int PostNo { get; private set; }
        public string[] Tags { get; private set; }
        public Rating Rated { get; private set; }


        public BooruPost(Uri uri, Exception ex) : base(uri, ex) {}

        public BooruPost(Uri uri, int postNo, string tags, string rated) : base(uri)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Cannot be 0 or negative.");

            PostNo = postNo;
            Tags = Split(tags);
            Rated = RatingStringToEnum(rated);
        }


        public static string[] Split(string tags)
        {
            if (tags != null)
                return tags.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            else
                return new string[0];
        }

        static Rating RatingStringToEnum(string rating)
        {
            switch(rating)
            {
            case "s":
                return Rating.Safe;
            case "q":
                return Rating.Questionable;
            case "e":
                return Rating.Explicit;
            default:
                return Rating.Unknown;
            }
        }
    }
}