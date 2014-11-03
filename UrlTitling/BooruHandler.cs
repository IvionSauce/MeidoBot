using System;
using System.Collections.Generic;
using IvionWebSoft;


namespace WebIrc
{
    abstract public class BooruHandler
    {
        public HashSet<string> WarningTags { get; set; }

        public string NormalCode { get; set; }


        // NormalCode, Rating, Warning and PostInfo/PostNo.
        const string msgFormat = "{0}[{1}]{2} [ {3} ]";


        protected string FormatMessage(BooruPost.Rating rated, string warning, int postNo)
        {
            var postId = string.Concat( "#", postNo.ToString() );
            return FormatMessage(rated, warning, postId);
        }

        protected string FormatMessage(BooruPost.Rating rated, string warning, string info)
        {
            string rating = ResolveRating(rated);
            return string.Format(msgFormat, NormalCode, rating, warning, info);
        }


        static string ResolveRating(BooruPost.Rating rating)
        {
            switch(rating)
            {
            case BooruPost.Rating.Safe:
                return "s";
            case BooruPost.Rating.Questionable:
                return "q";
            default:
                return "e";
            }
        }


        protected string ConstructWarning(string[] generalTags)
        {
            // Return early if there's nothing to do.
            if (WarningTags == null || WarningTags.Count == 0 || generalTags.Length == 0)
                return string.Empty;
            
            var warnings = new List<string>();
            foreach (string tag in generalTags)
                if (WarningTags.Contains(tag))
                    warnings.Add(tag);
            
            if (warnings.Count > 0)
                return string.Concat( " [", string.Join(", ", warnings), "]" );
            else
                return string.Empty;
        }
    }



    public class DanboHandler : BooruHandler
    {
        public int MaxTagCount { get; set; }
        public string ContinuationSymbol { get; set; }
        public bool Colourize { get; set; }
        
        string[] codes = {"\u000303", "\u000306", "\u000305"};
        public string CharacterCode
        {
            get { return codes[0]; }
            set { codes[0] = value; }
        }
        public string CopyrightCode
        {
            get { return codes[1]; }
            set { codes[1] = value; }
        }
        public string ArtistCode
        {
            get { return codes[2]; }
            set { codes[2] = value; }
        }

        const string resetCode = "\u000F";
        
        
        public TitlingResult PostToIrc(TitlingRequest req)
        {
            BooruPost postInfo = DanboTools.GetPostInfo(req.Url);
            req.Resource = postInfo;
            
            if (postInfo.Success)
            {
                string warning = ConstructWarning(postInfo.GeneralTags);
                
                // If image has no character, copyright or artist tags, return just the post ID, rating and
                // possible warning.
                if (postInfo.CopyrightTags.Length == 0 &&
                    postInfo.CharacterTags.Length == 0 &&
                    postInfo.ArtistTags.Length == 0)
                {
                    req.ConstructedTitle = FormatMessage(postInfo.Rated, warning, postInfo.PostNo);
                    return req.CreateResult(true);;
                }
                
                DanboTools.CleanupCharacterTags(postInfo.CharacterTags, postInfo.CopyrightTags);
                
                // Convert to string and limit the number of tags as specified in `MaxTagCount`.
                // Also colourize the tags if set to true.
                var characters = TagArrayToString(postInfo.CharacterTags, CharacterCode);
                var copyrights = TagArrayToString(postInfo.CopyrightTags, CopyrightCode);
                var artists = TagArrayToString(postInfo.ArtistTags, ArtistCode);
                
                string danbo = FormatDanboInfo(characters, copyrights, artists);
                req.ConstructedTitle = FormatMessage(postInfo.Rated, warning, danbo);

                return req.CreateResult(true);
            }
            else
                return req.CreateResult(false);
        }


        string TagArrayToString(string[] tags, string colour)
        {
            if (tags.Length == 0)
                return string.Empty;

            string joiner;
            if (Colourize)
                joiner = string.Concat(resetCode, NormalCode, ", ", colour);
            else
                joiner = ", ";

            string tagStr;
            if (MaxTagCount > 0 && tags.Length > MaxTagCount)
                tagStr = string.Concat( string.Join(joiner, tags, 0, MaxTagCount), ContinuationSymbol );
            else
                tagStr = string.Join(joiner, tags);

            if (Colourize)
                return string.Concat(colour, tagStr, resetCode, NormalCode);
            else
                return tagStr;
        }
        
        
        static string FormatDanboInfo(string characters, string copyrights, string artists)
        {
            string danbo = null;
            
            // If we have characters and copyrights, use them both. If we just have either characters or copyrights
            // use the one we have.
            if (!string.IsNullOrEmpty(characters))
            {                
                if (!string.IsNullOrEmpty(copyrights))
                    danbo = string.Format("{0} ({1})", characters, copyrights);
                else
                    danbo = characters;
            }
            else if (!string.IsNullOrEmpty(copyrights))
                danbo = copyrights;
            
            // Use the artists tags if we have them.
            if (!string.IsNullOrEmpty(artists))
            {
                // Dependent on whether we have the previous 2 (characters and copyrights) prepend the artist bit with
                // a space.
                if (danbo == null)
                    danbo = string.Concat("drawn by ", artists);
                else
                    danbo = string.Concat(danbo, " drawn by ", artists);
            }
            
            return danbo;
        }
    }


    public class GelboHandler : BooruHandler
    {
        public TitlingResult PostToIrc(TitlingRequest req)
        {
            BooruPost postInfo = GelboTools.GetPostInfo(req.Url);
            req.Resource = postInfo;

            if (postInfo.Success)
            {
                string warning = ConstructWarning(postInfo.AllTags);
                if (!string.IsNullOrEmpty(warning))
                {
                    req.ConstructedTitle = FormatMessage(postInfo.Rated, warning, postInfo.PostNo);
                    return req.CreateResult(true);
                }
            }
            return req.CreateResult(false);
        }
    }

}