using System;
using System.Collections.Generic;

namespace WebResources
{
    public class DanboPost : WebResource
    {
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


    public class ChanPost : WebResource
    {
        public string Board { get; private set; }
        public string BoardName { get; private set; }
        public int ThreadNo { get; private set; }
        public int PostNo { get; private set; }
        public string Subject { get; private set; }
        public string Comment { get; private set; }
        

        public ChanPost() : base() {}

        public ChanPost(WebResource resource) : base(resource) {}

        public ChanPost(WebResource resource,
                        string board, string boardName,
                        int threadNo, int postNo,
                        string subject, string comment) : base(resource)
        {            
            Board = board;
            BoardName = boardName;
            ThreadNo = threadNo;
            PostNo = postNo;
            Subject = subject;
            Comment = comment;
        }
    }


    public class WebString : WebResource
    {
        public string Document { get; private set; }
        
        
        public WebString(Uri uri, string doc) :
        this(uri, true, null, doc) {}
        
        public WebString(Uri uri, Exception ex) :
        this(uri, false, ex, null) {}
        
        public WebString(Uri uri) :
        this(uri, false, null, null) {}
        
        public WebString(Uri uri, bool succes, Exception ex, string doc) : base(uri, succes, ex)
        {
            Document = doc;
        }
    }


    public class WebResource
    {
        public Uri Location { get; private set; }
        public bool Succes { get; private set; }
        public Exception Exception { get; private set; }
        

        public WebResource()
        {
            Succes = false;
        }

        public WebResource(Uri uri, bool succes, Exception ex)
        {
            Location = uri;
            Succes = succes;
            Exception = ex;
        }

        public WebResource(WebResource resource)
        {
            Location = resource.Location;
            Succes = resource.Succes;
            Exception = resource.Exception;
        }
    }
}