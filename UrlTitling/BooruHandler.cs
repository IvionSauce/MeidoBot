using System.Collections.Generic;
using WebHelp;


namespace WebIrc
{
    abstract public class BooruHandler
    {
        public HashSet<string> WarningTags { get; set; }


        protected static string ResolveRating(BooruPost.Rating rating)
        {
            if (rating == BooruPost.Rating.Safe)
                return "s";
            else if (rating == BooruPost.Rating.Questionable)
                return "q";
            else
                return "e";
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
                return string.Format( "[Warning: {0}]", string.Join(", ", warnings) );
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
        
        string _normalCode = "";
        public string NormalCode
        {
            get { return _normalCode; }
            set { _normalCode = value; }
        }
        
        const string resetCode = "\u000F";
        
        
        public string PostToIrc(string url)
        {
            BooruPost postInfo = DanboTools.GetPostInfo(url);
            
            if (postInfo.Success)
            {
                string rating = ResolveRating(postInfo.Rated);
                string warning = ConstructWarning(postInfo.GeneralTags);
                
                // If image has no character, copyright or artist tags, return just the post ID and rating.
                if (postInfo.CopyrightTags.Length == 0 &&
                    postInfo.CharacterTags.Length == 0 &&
                    postInfo.ArtistTags.Length == 0)
                {
                    return string.Format("{0}[{1}] [ #{2} ] {3}", NormalCode, rating, postInfo.PostNo, warning);
                }
                
                string[] cleanedCharacters =
                    DanboTools.CleanupCharacterTags(postInfo.CharacterTags, postInfo.CopyrightTags);
                
                // Convert to string and limit the number of tags as specified in `MaxTagCount`.
                var characters = TagArrayToString(cleanedCharacters);
                var copyrights = TagArrayToString(postInfo.CopyrightTags);
                var artists = TagArrayToString(postInfo.ArtistTags);
                // Colourize the tags.
                if (Colourize)
                {
                    characters = ColourizeTags(characters, CharacterCode);
                    copyrights = ColourizeTags(copyrights, CopyrightCode);
                    artists = ColourizeTags(artists, ArtistCode);
                }
                
                string danbo = FormatDanboInfo(characters, copyrights, artists);
                
                return string.Format("{0}[{1}] [ {2} ] {3}", NormalCode, rating, danbo, warning);
            }
            else
            {
                postInfo.ReportError(url);
                return null;
            }
        }


        string TagArrayToString(string[] tags)
        {
            if (tags.Length > MaxTagCount)
                return string.Concat( string.Join(" ", tags, 0, MaxTagCount), ContinuationSymbol );
            else
                return string.Join(" ", tags);
        }
        
        
        string ColourizeTags(string tags, string colour)
        {
            if (string.IsNullOrEmpty(tags))
                return tags;
            else
                return string.Concat(colour, tags, resetCode, NormalCode);
        }
        
        
        static string FormatDanboInfo(string characters, string copyrights, string artists)
        {
            string danbo = "";
            
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
                if (danbo == "")
                    danbo = string.Concat("drawn by ", artists);
                else
                    danbo = string.Concat(danbo, " drawn by ", artists);
            }
            
            return danbo;
        }
    }


    public class GelboHandler : BooruHandler
    {
        public string PostToIrc(string url)
        {
            BooruPost postInfo = GelboTools.GetPostInfo(url);

            if (postInfo.Success)
            {
                string warning = ConstructWarning(postInfo.AllTags);
                if (!string.IsNullOrEmpty(warning))
                {
                    string rating = ResolveRating(postInfo.Rated);
                    return string.Format("[{0}] [ #{1} ] {2}", rating, postInfo.PostNo, warning);
                }
                else
                    return null;
            }
            else
            {
                postInfo.ReportError(url);
                return null;
            }
        }
    }
}