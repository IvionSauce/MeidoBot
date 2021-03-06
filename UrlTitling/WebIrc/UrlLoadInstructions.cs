﻿using System;


namespace WebIrc
{
    class UrlLoadInstructions
    {
        public readonly Predicate<Uri> Match;
        public readonly int FetchSize;
        public readonly bool FollowMetaRefreshes;
        public readonly Func<TitlingRequest, string, TitlingResult> Handler;


        public static UrlLoadInstructions Twitter;
        public static UrlLoadInstructions Youtube;


        static UrlLoadInstructions()
        {
            Twitter = new UrlLoadInstructions(
                uri => uri.Host.Equals("twitter.com", StringComparison.OrdinalIgnoreCase),
                SizeConstants.Twitter,
                (req, html) => req.CreateResult(true)
            );

            Youtube = new UrlLoadInstructions(
                uri =>
                uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase),
                SizeConstants.Youtube,
                MiscHandlers.YoutubeWithDuration
            );
        }


        public UrlLoadInstructions(
            Predicate<Uri> urlMatch,
            int fetchSize,
            Func<TitlingRequest, string, TitlingResult> handler) : this(urlMatch, fetchSize, false, handler) {}

        public UrlLoadInstructions(
            Predicate<Uri> urlMatch,
            int fetchSize,
            bool followRefreshes,
            Func<TitlingRequest, string, TitlingResult> handler)
        {
            Match = urlMatch;
            FetchSize = fetchSize;
            FollowMetaRefreshes = followRefreshes;
            Handler = handler;
        }
    }
}