using System;
using System.IO;
using System.Collections.Generic;

namespace IvionSoft.MinimalistParsers
{
    internal static class ExtensionMethods
    {
        public static uint ReadUint(this Stream stream, int byteWidth)
        {
            var bInt = new byte[byteWidth];
            stream.ReadInto(bInt);
            
            uint num = 0;
            foreach (byte b in bInt)
            {
                num <<= 8;
                num |= b;
            }
            return num;
        }
        
        
        public static bool ReadAndCompare(this Stream stream, IList<byte> bytes)
        {
            var possibleMatch = new byte[bytes.Count];
            stream.ReadInto(possibleMatch);
            
            return possibleMatch.StartsWith(bytes);
        }
        
        
        public static bool StartsWith(this IList<byte> a, IList<byte> b)
        {
            if (b.Count > a.Count)
                return false;
            
            for (int i = 0; i < b.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
        
        
        public static bool ReadInto(this Stream stream, byte[] buffer)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == buffer.Length)
                return true;
            else
                return false;
        }
    }
}