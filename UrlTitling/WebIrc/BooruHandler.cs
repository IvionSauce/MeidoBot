using System;
using System.Collections.Generic;
using IvionWebSoft;


namespace WebIrc
{
    abstract public class BooruHandler
    {
        public HashSet<string> WarningTags { get; set; }

        public string NormalCode { get; set; }


        protected void FormatMessage(TitleBuilder title, BooruPost.Rating rated, string warning, int postNo)
        {
            string rating = ResolveRating(rated);
            title.SetFormat("{0}[{1}]", NormalCode, rating);
            title.Append(warning).AppendFormat("[ #{0} ]", postNo);
        }

        protected void FormatMessage(TitleBuilder title, BooruPost.Rating rated, string warning)
        {
            string rating = ResolveRating(rated);
            title.SetFormat("{0}[{1}]", NormalCode, rating).Append(warning);
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
            return ConstructWarning(generalTags, new string[0]);
        }

        protected string ConstructWarning(string[] generalTags, string[] metaTags)
        {
            // Return early if there's nothing to do.
            if (WarningTags == null || WarningTags.Count == 0 ||
                (generalTags.Length == 0 && metaTags.Length == 0))
            {
                return string.Empty;
            }
            
            var warnings = new List<string>();
            WarningLoop(warnings, generalTags);
            WarningLoop(warnings, metaTags);
            
            if (warnings.Count > 0)
                return string.Concat("[", string.Join(", ", warnings), "]");
            else
                return string.Empty;
        }

        void WarningLoop(List<string> warnings, string[] tags)
        {
            foreach (string tag in tags)
                if (WarningTags.Contains(tag))
                    warnings.Add(tag);
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
        

        public TitlingResult HandleRequest(TitlingRequest req)
        {
            if (req.Url.Contains("donmai.us/posts/",
                                 StringComparison.OrdinalIgnoreCase))
            {
                return PostToIrc(req);
            }

            return null;
        }

        public TitlingResult PostToIrc(TitlingRequest req)
        {
            DanboPost postInfo = DanboTools.GetPostInfo(req.Url);
            req.Resource = postInfo;
            
            if (postInfo.Success)
            {
                string warning = ConstructWarning(postInfo.GeneralTags, postInfo.MetaTags);
                
                // If image has no character, copyright or artist tags, return just the post ID, rating and
                // possible warning.
                if (postInfo.CopyrightTags.Length == 0 &&
                    postInfo.CharacterTags.Length == 0 &&
                    postInfo.ArtistTags.Length == 0)
                {
                    FormatMessage(req.IrcTitle, postInfo.Rated, warning, postInfo.PostNo);
                    return req.CreateResult(true);
                }
                
                DanboTools.CleanupCharacterTags(postInfo.CharacterTags, postInfo.CopyrightTags);
                
                // Convert to string and limit the number of tags as specified in `MaxTagCount`.
                // Also colourize the tags if set to true.
                var characters = TagArrayToString(postInfo.CharacterTags, CharacterCode);
                var copyrights = TagArrayToString(postInfo.CopyrightTags, CopyrightCode);
                var artists = TagArrayToString(postInfo.ArtistTags, ArtistCode);

                FormatMessage(req.IrcTitle, postInfo.Rated, warning);
                FormatDanboInfo(req.IrcTitle, characters, copyrights, artists);

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
        
        
        static void FormatDanboInfo(TitleBuilder title, string characters, string copyrights, string artists)
        {
            title.Append('[');
            // If we have characters and copyrights, use them both. If we just have either characters or copyrights
            // use the one we have.
            title.Append(characters);
            if (!string.IsNullOrEmpty(copyrights))
                title.AppendFormat("({0})", copyrights);

            // Use the artists tags if we have them.
            if (!string.IsNullOrEmpty(artists))
                title.Append("drawn by").Append(artists);

            title.Append(']');
        }
    }


    public class GelboHandler : BooruHandler
    {

        public TitlingResult HandleRequest(TitlingRequest req)
        {
            if (req.Url.Contains("gelbooru.com/index.php?page=post&s=view&id=",
                                 StringComparison.OrdinalIgnoreCase))
            {
                return PostToIrc(req);
            }

            return null;
        }

        public TitlingResult PostToIrc(TitlingRequest req)
        {
            BooruPost postInfo = GelboTools.GetPostInfo(req.Url);
            req.Resource = postInfo;

            if (postInfo.Success)
            {
                string warning = ConstructWarning(postInfo.Tags);
                if (!string.IsNullOrEmpty(warning))
                {
                    FormatMessage(req.IrcTitle, postInfo.Rated, warning, postInfo.PostNo);
                    return req.CreateResult(true);
                }
            }
            return req.CreateResult(false);
        }
    }

}