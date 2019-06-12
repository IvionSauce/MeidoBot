namespace WebIrc
{
    // All these "constants" are subject to change in response to ongoing
    // performance testing, changing requirements and changes in upstream.
    // So it's nice to have these numbers and their justifications in one place.
    static class SizeConstants
    {
        // 16 KiB
        // By default we only need to get enough to read the <title>.
        public const int HtmlDefault = 16*1024;

        // 64 KiB
        // Some JPEGs embed a thumbnail, testing has shown that 64 KiB is
        // usually enough to get beyond the thumbnail at the start. For
        // most other formats it's too much, but it's fast enough.
        public const int NonHtmlDefault = 64*1024;

        // 16 KiB
        // We only need the <title>.
        public const int Twitter = 16*1024;
        // 1 MiB
        // We want to get the whole page, but limit it just to be sure.
        public const int Wikipedia = 1*1024*1024;
        // 96 KiB
        // We need to get the video duration embedded in the Javascript
        // for the video player, so we need to get a bit more than usual.
        public const int Youtube = 96*1024;
    }
}