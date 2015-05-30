using System;
using System.IO;


namespace IvionWebSoft
{
    public static class StreamExtensions
    {
        // Use 32K as the standard size for both fragment- and chunkSize.
        const int stdSize = 32*1024;


        public static byte[] ReadFragment(this Stream stream, int fragmentSize)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            else if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", "stream");

            byte[] fragment;
            if (fragmentSize > 0)
                fragment = new byte[fragmentSize];
            else
                fragment = new byte[stdSize];

            int read = 0;
            while (read < fragment.Length)
            {
                int chunk = stream.Read( fragment, read, (fragment.Length - read) );
                if (chunk > 0)
                    read += chunk;
                else
                    break;
            }

            if (read == fragment.Length)
                return fragment;
            else
            {
                var trimmedFragment = new byte[read];
                Array.Copy(fragment, trimmedFragment, read);
                return trimmedFragment;
            }
        }


        // http://www.yoda.arachsys.com/csharp/readbinary.html
        public static byte[] ReadFully(this Stream stream, int chunkSize)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            else if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", "stream");

            byte[] buffer;
            if (chunkSize > 0)
                buffer = new byte[chunkSize];
            else
                buffer = new byte[stdSize];

            // Total amount of bytes read.
            int read = 0;
            // Amount of bytes read each loop.
            int chunk;
            while ( (chunk = stream.Read(buffer, read, buffer.Length - read)) > 0 )
            {
                read += chunk;

                // If we've reached the end of our buffer, check to see if there's
                // any more information
                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();
                    // End of stream? If so, we're done.
                    if (nextByte == -1)
                        return buffer;

                    // Nope. Resize the buffer... 
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    // put in the byte we've just read...
                    newBuffer[read] = (byte)nextByte;
                    read++;
                    // and continue.
                    buffer = newBuffer;
                }
            }
            // Buffer is now too big. Shrink it.
            var trimmedBuffer = new byte[read];
            Array.Copy(buffer, trimmedBuffer, read);
            return trimmedBuffer;
        }
    }
}