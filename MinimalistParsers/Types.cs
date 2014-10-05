using System;

namespace MinimalistParsers
{
    public enum ImageType
    {
        NotSupported,
        Jpeg,
        Png
    };


    public struct ImageProperties
    {
        public readonly ImageType Type;
        public readonly Dimensions Dimensions;


        public ImageProperties(ImageType type)
        {
            Type = type;
            Dimensions = new Dimensions();
        }

        public ImageProperties(ImageType type, Dimensions dimensions)
        {
            Type = type;
            Dimensions = dimensions;
        }
    }


    public struct Dimensions
    {
        public readonly uint Width;
        public readonly uint Height;


        public Dimensions(uint width, uint height)
        {
            Width = width;
            Height = height;
        }

        public Dimensions(int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException("width", "Cannot be negative.");
            else if (height < 0)
                throw new ArgumentOutOfRangeException("height", "Cannot be negative.");

            Width = (uint)width;
            Height = (uint)height;
        }
    }
}