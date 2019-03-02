using System;
using System.Text;


namespace IvionWebSoft
{
    static class Sanitize
    {
        enum State
        {
            Begin,
            Reading,
            Squashing
        }


        // Removes leading and trailing whitespace, and squashes consecutive whitespace characters into 1 space.
        public static string SquashWhitespace(string s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            var builder = new StringBuilder(s.Length);

            int index = 0;
            var state = State.Begin;

            while (index < s.Length)
            {
                char c = s[index];

                switch (state)
                {
                    // Quickly skip over all the whitespace at the beginning of the string.
                    case State.Begin:
                    while ( index < s.Length && char.IsWhiteSpace(s[index]) )
                    {
                        index++;
                    }
                    state = State.Reading;
                    break;

                    // Read characters until we encounter whitespace...
                    case State.Reading:
                    if (char.IsWhiteSpace(c))
                    {
                        builder.Append(' ');
                        state = State.Squashing;
                    }
                    else
                        builder.Append(c);

                    index++;
                    break;

                    // Having encountered whitespace ignore all subsequent whitespace characters.
                    // Return to normal reading state upon encountering non-whitespace.
                    case State.Squashing:
                    if (!char.IsWhiteSpace(c))
                    {
                        builder.Append(c);
                        state = State.Reading;
                    }

                    index++;
                    break;
                }
            } // while

            // If we end in squashing state that means that the last character added was a space,
            // remove it.
            if (state == State.Squashing)
            {
                builder.Remove( (builder.Length - 1), 1 );
            }

            return builder.ToString();
        }
    }
}