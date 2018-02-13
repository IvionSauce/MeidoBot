using System;

namespace IvionWebSoft
{
    public class DanboPost : BooruPost
    {
        /// <summary>
        /// Copyright and/or franchise tags. Will be empty if no copyright tags.
        /// </summary>
        public string[] CopyrightTags { get; private set; }
        /// <summary>
        /// Character tags. Will be empty if no character tags.
        /// </summary>
        public string[] CharacterTags { get; private set; }
        /// <summary>
        /// Artist tags. Will be empty if no artist tags.
        /// </summary>
        public string[] ArtistTags { get; private set; }
        /// <summary>
        /// General tags, ie not copyright, character or artist tags.
        /// Will be empty if no general tags.
        /// </summary>
        public string[] GeneralTags { get; private set; }
        /// <summary>
        /// Meta tags, tags pertaining to the picture's meta qualities.
        /// Will be empty if no meta tags.
        /// </summary>
        public string[] MetaTags { get; private set; }


        internal DanboPost(WebResource resource) : base(resource) {}

        public DanboPost(Uri uri, Exception ex) : base(uri, ex) {}


        public DanboPost(Uri uri,
            int postNo,
            string copyrightTags,
            string characterTags,
            string artistTags,
            string generalTags,
            string metaTags,
            string allTags,
            string rated) : base(uri, postNo, allTags, rated)
        {
            CopyrightTags = Split(copyrightTags);
            CharacterTags = Split(characterTags);
            ArtistTags = Split(artistTags);
            GeneralTags = Split(generalTags);
            MetaTags = Split(metaTags);
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

        /// <summary>
        /// Post number.
        /// </summary>
        public int PostNo { get; private set; }
        /// <summary>
        /// All tags. Will be empty if no tags.
        /// </summary>
        public string[] Tags { get; private set; }
        /// <summary>
        /// Rating of the post.
        /// </summary>
        public Rating Rated { get; private set; }


        internal BooruPost(WebResource resource) : base(resource) {}

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