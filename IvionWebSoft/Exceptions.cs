using System;
using System.Net;

namespace IvionWebSoft
{
    [Serializable()]
    public class NotHtmlException : Exception
    {
        public NotHtmlException() : base() {}
        public NotHtmlException(string message) : base(message) {}
        public NotHtmlException(string message, Exception inner) : base(message, inner) {}
        
        protected NotHtmlException (System.Runtime.Serialization.SerializationInfo info,
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