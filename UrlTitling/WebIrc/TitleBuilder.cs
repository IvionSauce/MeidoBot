using System;
using System.Text;

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
                sb.Append(' ');

                var hours = (int)duration.TotalHours;
                int seconds = duration.Seconds;
                if (duration.Milliseconds >= 500)
                    seconds++;

                if (hours > 0)
                    sb.AppendFormat("[{0}:{1:00}:{2:00}]", hours, duration.Minutes, seconds);
                else
                    sb.AppendFormat("[{0}:{1:00}]", duration.Minutes, seconds);
            }
            return this;
        }

        public TitleBuilder AppendSize(long size)
        {
            if (size >= 1)
            {
                sb.Append(' ');
                var sizeInK = size / 1024d;
                if (sizeInK > 1024)
                {
                    var sizeInM = sizeInK / 1024d;
                    sb.Append(sizeInM.ToString("#.#"));
                    sb.Append("MB");
                }
                else
                {
                    sb.Append(sizeInK.ToString("#.#"));
                    sb.Append("KB");
                }
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