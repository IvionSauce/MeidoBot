using System;
using System.Net;

namespace IvionWebSoft
{
    [Serializable()]
    public class UrlNotHtmlException : Exception
    {
        public UrlNotHtmlException() : base() {}
        public UrlNotHtmlException(string message) : base(message) {}
        public UrlNotHtmlException(string message, Exception inner) : base(message, inner) {}
        
        protected UrlNotHtmlException (System.Runtime.Serialization.SerializationInfo info,
                                       System.Runtime.Serialization.StreamingContext context) {}
    }


    [Serializable()]
    public class JsonErrorException : Exception
    {
        public JsonErrorException() : base() {}
        public JsonErrorException(string message) : base(message) {}
        public JsonErrorException(string message, Exception inner) : base(message, inner) {}
        
        protected JsonErrorException (System.Runtime.Serialization.SerializationInfo info,
                                 System.Runtime.Serialization.StreamingContext context) {}
    }


    [Serializable()]
    public class JsonParseException : Exception
    {
        public JsonParseException() : base() {}
        public JsonParseException(string message) : base(message) {}
        public JsonParseException(string message, Exception inner) : base(message, inner) {}
        
        protected JsonParseException (System.Runtime.Serialization.SerializationInfo info,
                                      System.Runtime.Serialization.StreamingContext context) {}
    }
}