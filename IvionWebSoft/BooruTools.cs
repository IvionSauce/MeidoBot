using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.ComponentModel;
// JSON.NET
using Newtonsoft.Json;


namespace IvionWebSoft
{
    static class BooruTools
    {
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
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
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
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if postNo is &lt= 0.</exception>
        /// <param name="postNo">Post number.</param>
        public static DanboPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");
            
            var jsonReq = string.Format("http://sonohara.donmai.us/posts/{0}.json", postNo);
            var json = WebString.Download(jsonReq);
            if (!json.Success)
                return new DanboPost(json);
            
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
        /// 
        /// <exception cref="ArgumentNullException">Thrown if charTags or copyrightTags is null.</exception>
        /// 
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
            
            for (int i = 0; i < charTags.Length; i++)
            {
                string charTag = charTags[i];

                int sourceOpenIdx, sourceCloseIdx;
                if ( TryFindSourceIndices(charTag, out sourceOpenIdx, out sourceCloseIdx) )
                {
                    string source = ExtractSource(charTag, sourceOpenIdx, sourceCloseIdx);

                    if (RemoveSource(source, copyrightTags))
                    {
                        int len = (sourceCloseIdx - sourceOpenIdx) + 1;
                        charTags[i] = charTag.Remove(sourceOpenIdx, len);
                    }
                }
            } // for
        }

        static bool TryFindSourceIndices(string charTag, out int start, out int end)
        {
            start = FindSourceStart(charTag);
            if (start < 0)
            {
                end = -1;
                return false;
            }
            else
            {
                end = FindSourceEnd(charTag, start);
                return true;
            }
        }

        static int FindSourceStart(string charTag)
        {
            const string sourceStart = "_(";
            return charTag.IndexOf(sourceStart, StringComparison.Ordinal);
        }

        static int FindSourceEnd(string charTag, int sourceStart)
        {
            const string sourceEnd= ")";
            int end = charTag.IndexOf(sourceEnd, sourceStart, StringComparison.Ordinal);

            if (end > sourceStart)
                return end;
            else
                return charTag.Length - 1;
        }

        static string ExtractSource(string charTag, int sourceStart, int sourceEnd)
        {
            // Plus 2 to skip past the "_(" part of the source.
            int start = sourceStart + 2;
            int len = sourceEnd - start;

            return charTag.Substring(start, len);
        }

        static bool RemoveSource(string source, string[] copyrightTags)
        {
            foreach (string copyTag in copyrightTags)
            {
                if (RemoveSource(source, copyTag))
                    return true;
            }

            return false;
        }

        static bool RemoveSource(string source, string copyrightTag)
        {
            // Slice off the source if a copyright tag contains it. Examples:
            // _(kantai_collection) is in kantai_collection
            // _(jojo) is in jojo_no_kimyou_na_bouken
            // But also check if the source starts with a copyright tag, this is less common. Examples:
            // _(gundam_bf) starts with gundam
            if (copyrightTag.Contains(source, StringComparison.Ordinal))
                return true;
            if (source.StartsWith(copyrightTag, StringComparison.Ordinal))
                return true;

            return false;
        }
    }


    public static class GelboTools
    {
        static readonly Regex gelboUrlRegexp = new Regex(@"(?i)gelbooru.com/index.php\?page=post&s=view&id=(\d+)");


        /// <summary>
        /// Get info of a Gelbooru post.
        /// </summary>
        /// <returns><see cref="BooruPost">BooruPost</see> detailing a post.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL pointing to a post.</param>
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


        /// <summary>
        /// Get info of a Gelbooru post.
        /// </summary>
        /// <returns><see cref="BooruPost">BooruPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if postNo is &lt= 0.</exception>
        /// <param name="postNo">Post number.</param>
        public static BooruPost GetPostInfo(int postNo)
        {
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("postNo", "Can't be 0 or negative.");

            var xmlReq = string.Format("http://gelbooru.com/index.php?page=dapi&s=post&q=index&id={0}", postNo);
            var xml = WebString.Download(xmlReq);
            if (!xml.Success)
                return new BooruPost(xml);

            var postXml = XElement.Parse(xml.Document).Element("post");
            string tags = postXml.Attribute("tags").Value;
            string rated = postXml.Attribute("rating").Value;

            return new BooruPost(xml.Location, postNo, tags, rated);
        }
    }
}