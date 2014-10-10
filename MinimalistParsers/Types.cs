using System;

namespace MinimalistParsers
{
    public enum MediaType
    {
        NotSupported,
        Jpeg,
        Png,
        Matroska,
        Webm
    };


    public class MediaProperties
    {
        public readonly MediaType Type;
        public readonly Dimensions Dimensions;
        public readonly TimeSpan Duration;


        public MediaProperties()
        {
            // Leave all fields at default value.
        }

        public MediaProperties(MediaType type)
        {
            Type = type;
        }

        public MediaProperties(MediaType type, Dimensions dimensions) : this(type)
        {
            Dimensions = dimensions;
        }

        public MediaProperties(MediaType type, Dimensions dimensions, TimeSpan duration) : this(type, dimensions)
        {
            Duration = duration;
        }
    }


    public struct Dimensions
    {
        public readonly ulong Width;
        public readonly ulong Height;


        public Dimensions(uint width, uint height)
        {
            Width = width;
            Height = height;
        }

        public Dimensions(ulong width, ulong height)
        {
            Width = width;
            Height = height;
        }

        public Dimensions(int width, int height) : this((long)width, (long)height) {}

        public Dimensions(long width, long height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException("width", "Cannot be negative.");
            else if (height < 0)
                throw new ArgumentOutOfRangeException("height", "Cannot be negative.");

            Width = (ulong)width;
            Height = (ulong)height;
        }
    }

}