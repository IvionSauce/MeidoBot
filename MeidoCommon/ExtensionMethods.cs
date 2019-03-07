using System.Linq;
using System.Collections.Generic;


namespace MeidoCommon.ExtensionMethods
{
    public static class ExtensionMethods
    {
        public static IEnumerable<T> NoNull<T>(this IEnumerable<T> seq) where T : class
        {
            if (seq != null)
                return seq.Where(el => el != null);
            else
                return new T[0];
        }
    }
}