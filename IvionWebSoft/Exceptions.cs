using System;

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
    public class JsonException : Exception
    {
        public JsonException() : base() {}
        public JsonException(string message) : base(message) {}
        public JsonException(string message, Exception inner) : base(message, inner) {}
        
        protected JsonException (System.Runtime.Serialization.SerializationInfo info,
                                 System.Runtime.Serialization.StreamingContext context) {}
    }
}