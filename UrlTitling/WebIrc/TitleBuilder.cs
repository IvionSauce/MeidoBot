using System;
using System.Text;
using MeidoCommon.Formatting;


namespace WebIrc
{
    public class TitleBuilder
    {
        string _title = string.Empty;
        public string HtmlTitle
        {
            get { return _title; }
            set
            {
                if (value != null)
                    _title = value;
                else
                    _title = string.Empty;
            }
        }

        readonly StringBuilder sb;


        public TitleBuilder()
        {
            sb = new StringBuilder(256);
        }


        public TitleBuilder Append(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                sb.Append(' ');
                sb.Append(s);
            }
            return this;
        }

        public TitleBuilder Append(char c)
        {
            sb.Append(' ').Append(c);
            return this;
        }

        public TitleBuilder AppendFormat(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(format))
            {
                sb.Append(' ');
                sb.AppendFormat(format, args);
            }
            return this;
        }


        public TitleBuilder Set(string s)
        {
            sb.Clear();
            if (!string.IsNullOrEmpty(s))
            {
                sb.Append(s);
            }
            return this;
        }

        public TitleBuilder SetFormat(string format, params object[] args)
        {
            sb.Clear();
            if (!string.IsNullOrEmpty(format))
            {
                sb.AppendFormat(format, args);
            }
            return this;
        }

        public TitleBuilder SetHtmlTitle()
        {
            sb.Clear();
            sb.Append(string.Concat("[ ", HtmlTitle, " ]"));

            return this;
        }


        public TitleBuilder AppendTime(TimeSpan duration)
        {
            if (duration.TotalSeconds >= 0.5d)
            {
                sb.Append(' ').Append('[');
                sb.Append( Format.Duration(duration) );
                sb.Append(']');
            }
            return this;
        }

        public TitleBuilder AppendSize(long size)
        {
            if (size >= 1)
            {
                sb.Append(' ');
                sb.Append( Format.Size(size) );
            }
            return this;
        }


        public override string ToString()
        {
            if (sb.Length > 0)
                return sb.ToString();
            else
                return string.Concat("[ ", HtmlTitle, " ]");
        }
    }
}