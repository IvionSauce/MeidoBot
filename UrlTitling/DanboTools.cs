using System;
using System.Collections.Generic;
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


        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// <param name="url">URL pointing to a post.</param>
        public static DanboPost GetPostInfo(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            int postNo = ExtractPostNo(url);
            if (postNo > 0)
                return GetPostInfo(postNo);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Post No. from URL.");
                return new DanboPost(ex);
            }
        }

        static int ExtractPostNo(string url)
        {
            GroupCollection groups = danboUrlRegexp.Match(url).Groups;
            
            if (groups[1].Success)
                return int.Parse(groups[1].Value);
            else
                return -1;
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
            WebString json = WebTools.SimpleGetString(jsonReq);
            if (!json.Success)
                return new DanboPost(json);
            
            dynamic postJson = JsonConvert.DeserializeObject(json.Document);
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
            
            var postInfo = new DanboPost(json, postNo,
                                         copyrights.Split(' '),
                                         characters.Split(' '),
                                         artists.Split(' '),
                                         other.Split(' '),
                                         all.Split(' '),
                                         rated);
            
            return postInfo;
        }

        
        /// <summary>
        /// Shortens an array of tags.
        /// </summary>
        /// <returns>An array of strings equal or shorter than amount. Returns as-is if amount &lt= 0.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="amount">Maximum amount of items the array should have. Disable by passing &lt= 0.</param>
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
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if charTags is null.</exception>
        /// <param name="charTags">A tag array of character tags.</param>
        public static string[] CleanupCharacterTags(string[] charTags, string[] sourceTags)
        {
            if (charTags == null)
                throw new ArgumentNullException("charTags");

            // Return early if there's nothing to be done.
            if (charTags.Length == 0 || sourceTags.Length == 0)
                return charTags;

            string checkAgainst, charTag;
            int sourceStart;
            var filtered = new string[charTags.Length];
            foreach (string srcTag in sourceTags)
            {
                checkAgainst = string.Concat("_(", srcTag, ")");
                for (int i = 0; i < charTags.Length; i++)
                {
                    charTag = charTags[i];

                    sourceStart = charTag.IndexOf(checkAgainst);
                    if (sourceStart > 0)
                        filtered[i] = charTag.Substring(0, sourceStart);
                    else
                        filtered[i] = charTag;
                }
            }

            return filtered;
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

        
        public DanboPost(WebResource resource) : base(resource) {}

        public DanboPost(Exception ex) : base(null, false, ex) {}
        
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