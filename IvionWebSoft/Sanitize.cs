using System.Text;


namespace IvionWebSoft
{
    static class Sanitize
    {
        enum State
        {
            Reading,
            Squashing
        }


        // Removes leading and trailing whitespace, and squashes consecutive whitespace characters into 1 space.
        public static string SquashWhitespace(string s)
        {
            var builder = new StringBuilder(s.Length);

            // Quickly skip over all the whitespace at the beginning of the string.
            int index = FirstNonWhitespace(s);
            var state = State.Reading;

            while (index < s.Length)
            {
                char c = s[index];

                switch (state)
                {
                    // Read characters until we encounter whitespace...
                    case State.Reading:
                    if (char.IsWhiteSpace(c))
                    {
                        builder.Append(' ');
                        state = State.Squashing;
                    }
                    else if (!char.IsControl(c))
                        builder.Append(c);
                    
                    index++;
                    break;

                    // Having encountered whitespace ignore all subsequent whitespace characters.
                    // Return to normal reading state upon encountering non-whitespace.
                    case State.Squashing:
                    if (char.IsWhiteSpace(c))
                        index++;
                    else
                        state = State.Reading;
                    break;
                }
            }

            // If we end in squashing state that means that the last character added was a space,
            // remove it.
            if (state == State.Squashing)
            {
                builder.Remove( (builder.Length - 1), 1 );
            }

            return builder.ToString();
        }

        static int FirstNonWhitespace(string s)
        {
            int index = 0;
            while ( index < s.Length && char.IsWhiteSpace(s[index]) )
            {
                index++;
            }

            return index;
        }
    }
}