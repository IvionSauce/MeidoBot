using System;
using System.Text.RegularExpressions;
using IvionSoft;
// JSON.NET
using Newtonsoft.Json;


namespace WebHelp
{
    /// <summary>
    /// Collection of tools dealing with Danbooru.
    /// </summary>
    public static class DanboTools
    {
        static readonly Regex danboUrlRegexp = new Regex(@"(?i)donmai.us/posts/(\d+)");
        
        // Matches the "_(source)" part that is sometimes present with character tags.
        static readonly Regex charSourceRegexp = new Regex(@"_\([^) ]+\)");
        
        
        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <param name="url">URL pointing to a post.</param>
        public static DanboPost GetPostInfo(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");
            
            string postNo = GetPostNo(url);
            
            if (postNo == null)
                return new DanboPost();
            else
            {
                int post = int.Parse(postNo);
                if (post > 0)
                    return GetPostInfo(int.Parse(postNo));
                else
                    return new DanboPost();
            }
        }

        static string GetPostNo(string url)
        {
            GroupCollection groups = danboUrlRegexp.Match(url).Groups;
            
            if (groups[1].Success == true)
                return groups[1].Value;
            else
                return null;
        }

        
        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if postNo is &lt= 0.</exception>
        /// <param name="postNo">Post number.</param>
        public static DanboPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");
            
            var jsonReq = string.Format("http://sonohara.donmai.us/posts/{0}.json", postNo);
            WebString jsonStr = WebTools.SimpleGetString(jsonReq);
            if (!jsonStr.Success)
                return new DanboPost(jsonStr);
            
            dynamic postJson = JsonConvert.DeserializeObject(jsonStr.Document);
            string copyrights = postJson.tag_string_copyright;
            string characters = postJson.tag_string_character;
            string artists = postJson.tag_string_artist;
            string other = postJson.tag_string_general;
            string all = postJson.tag_string;
            
            DanboPost.Rating rated;
            if (postJson.rating == "s")
                rated = DanboPost.Rating.Safe;
            else if (postJson.rating == "q")
                rated = DanboPost.Rating.Questionable;
            else
                rated = DanboPost.Rating.Explicit;
            
            var postInfo = new DanboPost(jsonStr, postNo,
                                         copyrights.Split(' '),
                                         characters.Split(' '),
                                         artists.Split(' '),
                                         other.Split(' '),
                                         all.Split(' '),
                                         rated);
            
            return postInfo;
        }


        /// <summary>
        /// Converts an array of tags into a tag string.
        /// </summary>
        /// <returns>A string of tags.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="maxTags">Don't make the returned string contain more tags than maxTags.
        /// Disable by passing <= 0.</param>
        public static string TagArrayToString(string[] tags, int maxTags)
        {
            return TagArrayToString(tags, maxTags, "");
        }
        
        /// <summary>
        /// Converts an array of tags into a tag string.
        /// </summary>
        /// <returns>A string of tags.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="maxTags">Don't make the returned string contain more tags than maxTags.
        /// Disable by passing <= 0.</param>
        /// <param name="contSymbol">String to append to the returned string if it was shortened.</param>
        public static string TagArrayToString(string[] tags, int maxTags, string contSymbol)
        {
            string[] shortened = ShortenTagArray(tags, maxTags);
            var tagStr = string.Join(" ", shortened);
            
            if (shortened.Length != tags.Length)
                return string.Concat(tagStr, contSymbol);
            else
                return tagStr;
        }
        
        /// <summary>
        /// Shortens an array of tags.
        /// </summary>
        /// <returns>An array of strings equal or shorter than amount.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="amount">Maximum amount of items the array should have.</param>
        public static string[] ShortenTagArray(string[] tags, int amount)
        {
            if (tags == null)
                throw new ArgumentNullException("tags");
            
            if ( amount > 0 && tags.Length > amount )
            {
                var shortened = new string[amount];
                for (int i = 0; i < amount; i++)
                    shortened[i] = tags[i];
                
                return shortened;
            }
            else
                return tags;
        }
        
        
        /// <summary>
        /// Cleans up the character tags. Removes the "_(source)" part of the tags.
        /// Returns charTags as-is if null or empty.
        /// </summary>
        /// <returns>Cleaned up character tags.</returns>
        /// <param name="charTags">A tag string of character tags.</param>
        public static string CleanupCharacterTags(string charTags)
        {
            if (string.IsNullOrEmpty(charTags))
                return charTags;
            else
                return charSourceRegexp.Replace(charTags, "");
        }
        
        /// <summary>
        /// Cleans up the character tags. Removes the "_(source)" part of the tags.
        /// Modifies charTags in-place.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if charTags is null.</exception>
        /// <param name="charTags">A tag array of character tags.</param>
        public static void CleanupCharacterTags(string[] charTags)
        {
            if (charTags == null)
                throw new ArgumentNullException("charTags");
            
            for (int i = 0; i < charTags.Length; i++)
                charTags[i] = charSourceRegexp.Replace(charTags[i], "");
        }
    }


    /// <summary>
    /// Contains a Success bool which tells you if the request succeeded. If an expected exception occurred you can
    /// check the Exception property. If Exception is null and Succes is false it means something went wrong extracting
    /// the post number from the URL.
    /// </summary>
    public class DanboPost : WebResource
    {
        [Flags]
        public enum Rating
        {
            Safe,
            Questionable,
            Explicit
        }
        
        public int PostNo { get; private set; }
        public string[] CopyrightTags { get; private set; }
        public string[] CharacterTags { get; private set; }
        public string[] ArtistTags { get; private set; }
        public string[] GeneralTags { get; private set; }
        public string[] AllTags { get; private set; }
        public Rating Rated { get; private set; }
        
        
        public DanboPost() : base() {}
        
        public DanboPost(WebResource resource) : base(resource) {}
        
        public DanboPost(WebResource resource,
                         int postNo,
                         string[] copyrights,
                         string[] characters,
                         string[] artists,
                         string[] others,
                         string[] all,
                         Rating rated) : base(resource)
        {
            PostNo = postNo;
            CopyrightTags = copyrights;
            CharacterTags = characters;
            ArtistTags = artists;
            GeneralTags = others;
            AllTags = all;
            Rated = rated;
        }
    }
}