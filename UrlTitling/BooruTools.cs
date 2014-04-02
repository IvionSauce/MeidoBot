using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.ComponentModel;
using IvionSoft;
// JSON.NET
using Newtonsoft.Json;


namespace WebHelp
{
    /// <summary>
    /// Generic tools for use with Booru's.
    /// </summary>
    static class BooruTools
    {
        public enum Source
        {
            Danbooru,
            Gelbooru
        }

        static readonly Regex danboUrlRegexp = new Regex(@"(?i)donmai.us/posts/(\d+)");
        static readonly Regex gelboUrlRegexp = new Regex(@"(?i)gelbooru.com/index.php\?page=post&s=view&id=(\d+)");


        /// <summary>
        /// Extracts the post number from an URL.
        /// </summary>
        /// <returns>The post number. Returns -1 if number couldn't be found.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL</param>
        /// <param name="source">Source</param>
        public static int ExtractPostNo(string url, Source source)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            GroupCollection groups;
            switch(source)
            {
            case Source.Danbooru:
                groups = danboUrlRegexp.Match(url).Groups;
                break;
            case Source.Gelbooru:
                groups = gelboUrlRegexp.Match(url).Groups;
                break;
            default:
                throw new InvalidEnumArgumentException();
            }
            
            if (groups[1].Success)
                return int.Parse(groups[1].Value);
            else
                return -1;
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
    }


    /// <summary>
    /// Collection of tools dealing with Danbooru.
    /// </summary>
    public static class DanboTools
    {
        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="BooruPost">BooruPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// <param name="url">URL pointing to a post.</param>
        public static BooruPost GetPostInfo(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            int postNo = BooruTools.ExtractPostNo(url, BooruTools.Source.Danbooru);
            if (postNo > 0)
                return GetPostInfo(postNo);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Post No. from URL.");
                return new BooruPost(ex);
            }
        }

        
        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="BooruPost">BooruPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if postNo is &lt= 0.</exception>
        /// <param name="postNo">Post number.</param>
        public static BooruPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");
            
            var jsonReq = string.Format("http://sonohara.donmai.us/posts/{0}.json", postNo);
            WebString json = WebTools.SimpleGetString(jsonReq);
            if (!json.Success)
                return new BooruPost(json);
            
            dynamic postJson = JsonConvert.DeserializeObject(json.Document);
            string copyrights = postJson.tag_string_copyright;
            string characters = postJson.tag_string_character;
            string artists = postJson.tag_string_artist;
            string other = postJson.tag_string_general;
            string all = postJson.tag_string;
            string rating = postJson.rating;
            
            var postInfo = new BooruPost(json, postNo,
                                         copyrights.Split(' '),
                                         characters.Split(' '),
                                         artists.Split(' '),
                                         other.Split(' '),
                                         all.Split(' '),
                                         rating);
            
            return postInfo;
        }

        
        /// <summary>
        /// Cleans up the character tags. Removes the "_(source)" part of the tags.
        /// Modifies charTags in place.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if charTags is null.</exception>
        /// <param name="charTags">A tag array of character tags.</param>
        /// <param name="sourceTags">A tag array of copyright tags.</param>
        public static void CleanupCharacterTags(string[] charTags, string[] sourceTags)
        {
            if (charTags == null)
                throw new ArgumentNullException("charTags");

            // Return early if there's nothing to be done.
            if (charTags.Length == 0 || sourceTags.Length == 0)
                return;

            string checkAgainst, charTag;
            int sourceStart;
            foreach (string srcTag in sourceTags)
            {
                checkAgainst = string.Concat("_(", srcTag, ")");

                for (int i = 0; i < charTags.Length; i++)
                {
                    charTag = charTags[i];

                    sourceStart = charTag.IndexOf(checkAgainst);
                    // Only replace a tag if we removed the source part. Else we could overwrite a previously filtered
                    // charTag, that was fixed in a previous loop with another srcTag.
                    if (sourceStart > 0)
                        charTags[i] = charTag.Substring(0, sourceStart);
                }
            }
        }
    }


    public static class GelboTools
    {
        public static BooruPost GetPostInfo(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");
            
            int postNo = BooruTools.ExtractPostNo(url, BooruTools.Source.Gelbooru);
            if (postNo > 0)
                return GetPostInfo(postNo);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Post No. from URL.");
                return new BooruPost(ex);
            }
        }


        public static BooruPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");

            var xmlReq = string.Format("http://gelbooru.com/index.php?page=dapi&s=post&q=index&id={0}", postNo);
            WebString xml = WebTools.SimpleGetString(xmlReq);
            if (!xml.Success)
                return new BooruPost(xml);

            var postXml = XElement.Parse(xml.Document).Element("post");
            string tags = postXml.Attribute("tags").Value;
            string rated = postXml.Attribute("rating").Value;

            var postInfo = new BooruPost(xml,
                                         postNo,
                                         tags.Split(' '),
                                         rated);
            return postInfo;
        }
    }


    /// <summary>
    /// Contains a Success bool which tells you if the request succeeded. If an expected exception occurred you can
    /// check the Exception property.
    /// </summary>
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
        public string[] CopyrightTags { get; private set; }
        public string[] CharacterTags { get; private set; }
        public string[] ArtistTags { get; private set; }
        public string[] GeneralTags { get; private set; }
        public string[] AllTags { get; private set; }
        public Rating Rated { get; private set; }

        
        public BooruPost(WebResource resource) : base(resource) {}

        public BooruPost(Exception ex) : base(null, false, ex) {}

        // Smaller subset for less feature-rich booru's (Gelbooru).
        public BooruPost(WebResource resource,
                         int postNo,
                         string[] all,
                         string rated) : base(resource)
        {
            PostNo = postNo;
            AllTags = all;
            Rated = RatingStringToEnum(rated);
        }

        // Full set for Danbooru.
        public BooruPost(WebResource resource,
                         int postNo,
                         string[] copyrights,
                         string[] characters,
                         string[] artists,
                         string[] others,
                         string[] all,
                         string rated) : base(resource)
        {
            PostNo = postNo;
            CopyrightTags = copyrights;
            CharacterTags = characters;
            ArtistTags = artists;
            GeneralTags = others;
            AllTags = all;
            Rated = RatingStringToEnum(rated);
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