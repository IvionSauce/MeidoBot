using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.ComponentModel;
// JSON.NET
using Newtonsoft.Json;


namespace IvionWebSoft
{
    /// <summary>
    /// Generic tools for use with Booru's.
    /// </summary>
    static class BooruTools
    {
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
        internal static int ExtractPostNo(Regex urlRegexp, string url)
        {
            var groups = urlRegexp.Match(url).Groups;
            if (groups[1].Success)
                return int.Parse(groups[1].Value);
            else
                return -1;
        }
    }


    public static class DanboTools
    {
        static readonly Regex danboUrlRegexp = new Regex(@"(?i)donmai.us/posts/(\d+)");


        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="BooruPost">BooruPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// <param name="url">URL pointing to a post.</param>
        public static DanboPost GetPostInfo(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            int postNo = BooruTools.ExtractPostNo(danboUrlRegexp, url);
            if (postNo > 0)
                return GetPostInfo(postNo);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Post No. from URL.");
                return new DanboPost(null, ex);
            }
        }

        
        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="BooruPost">BooruPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if postNo is &lt= 0.</exception>
        /// <param name="postNo">Post number.</param>
        public static DanboPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");
            
            var jsonReq = string.Format("http://sonohara.donmai.us/posts/{0}.json", postNo);
            var json = WebString.Download(jsonReq);
            if (!json.Success)
                return new DanboPost(json.Location, json.Exception);
            
            dynamic postJson = JsonConvert.DeserializeObject(json.Document);
            string copyrights = postJson.tag_string_copyright;
            string characters = postJson.tag_string_character;
            string artists = postJson.tag_string_artist;
            string general = postJson.tag_string_general;
            string all = postJson.tag_string;
            string rating = postJson.rating;
            
            return new DanboPost(json.Location, postNo,
                copyrights, characters, artists, general, all, rating);
        }

        
        /// <summary>
        /// Cleans up the character tags. Removes the "_(source)" part of the tags.
        /// Modifies charTags in place.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if charTags or copyrightTags is null.</exception>
        /// <param name="charTags">A tag array of character tags.</param>
        /// <param name="copyrightTags">A tag array of copyright tags.</param>
        public static void CleanupCharacterTags(string[] charTags, string[] copyrightTags)
        {
            if (charTags == null)
                throw new ArgumentNullException("charTags");
            else if (copyrightTags == null)
                throw new ArgumentNullException("copyrightTags");

            // Return early if there's nothing to be done.
            if (charTags.Length == 0 || copyrightTags.Length == 0)
                return;

            const string sourceStart = "_(";
            const string sourceEnd= ")";
            for (int i = 0; i < charTags.Length; i++)
            {
                string charTag = charTags[i];

                int sourceOpenIdx = charTag.IndexOf(sourceStart, StringComparison.Ordinal);
                if (sourceOpenIdx > 0)
                {
                    int sourceCloseIdx = charTag.IndexOf(sourceEnd, sourceOpenIdx, StringComparison.Ordinal);
                    if (sourceCloseIdx < 0)
                        sourceCloseIdx = charTag.Length - 1;

                    // Plus 2 to skip past the "_(" part of the source.
                    int start = sourceOpenIdx + 2;
                    int len = sourceCloseIdx - start;
                    string source = charTag.Substring(start, len);

                    foreach (string srcTag in copyrightTags)
                    {
                        // Slice off the source if a copyright tag contains it. Examples:
                        // _(kantai_collection) is in kantai_collection
                        // _(jojo) is in jojo_no_kimyou_na_bouken
                        // But also check if the source starts with a copyright tag, this is less common. Examples:
                        // _(gundam_bf) starts with gundam
                        if (srcTag.Contains(source, StringComparison.Ordinal) ||
                            source.StartsWith(srcTag, StringComparison.Ordinal))
                        {
                            len = (sourceCloseIdx - sourceOpenIdx) + 1;
                            charTags[i] = charTag.Remove(sourceOpenIdx, len);
                        }
                    } // foreach
                } // if
            } // for
        }

    }


    public static class GelboTools
    {
        static readonly Regex gelboUrlRegexp = new Regex(@"(?i)gelbooru.com/index.php\?page=post&s=view&id=(\d+)");


        public static BooruPost GetPostInfo(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");
            
            int postNo = BooruTools.ExtractPostNo(gelboUrlRegexp, url);
            if (postNo > 0)
                return GetPostInfo(postNo);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Post No. from URL.");
                return new BooruPost(null, ex);
            }
        }


        public static BooruPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");

            var xmlReq = string.Format("http://gelbooru.com/index.php?page=dapi&s=post&q=index&id={0}", postNo);
            var xml = WebString.Download(xmlReq);
            if (!xml.Success)
                return new BooruPost(xml.Location, xml.Exception);

            var postXml = XElement.Parse(xml.Document).Element("post");
            string tags = postXml.Attribute("tags").Value;
            string rated = postXml.Attribute("rating").Value;

            return new BooruPost(xml.Location, postNo, tags, rated);
        }
    }
}