using System;
using System.IO;


namespace MeidoBot
{
    public enum LogRotateSchedule
    {
        None,
        Daily,
        Monthly,
        Yearly
    }


    class ChatlogMetaData
    {
        public string LogfilePath { get; private set; }
        public readonly LogRotateSchedule Schedule;
        public DateTimeOffset LastWrite { get; set; }

        readonly string barePath;


        public ChatlogMetaData(
            string basePath,
            string ircEntity,
            LogRotateSchedule schedule,
            DateTimeOffset now)
        {
            barePath = Path.Combine(
                basePath,
                // Sanitize name of irc entity so it's a valid filename.
                // This will probably be expanded in the future.
                ircEntity.Replace('/', '_')
            );
            Schedule = schedule;

            LogfilePath = DatedFilepath(barePath, now, schedule);
            LastWrite = DateTimeOffset.MaxValue;
        }


        public bool LogRotate(DateTimeOffset now)
        {
            bool rotate = false;

            if (now.Date > LastWrite.Date)
            {
                switch (Schedule)
                {
                    case LogRotateSchedule.Daily:
                    rotate |= now.Day != LastWrite.Day;
                    break;

                    case LogRotateSchedule.Monthly:
                    rotate |= now.Month != LastWrite.Month;
                    break;

                    case LogRotateSchedule.Yearly:
                    rotate |= now.Year != LastWrite.Year;
                    break;
                }
            }

            if (rotate)
                LogfilePath = DatedFilepath(barePath, now, Schedule);

            return rotate;
        }

        static string DatedFilepath(string path, DateTimeOffset date, LogRotateSchedule schedule)
        {
            const string seperator = " ";
            const string extension = ".txt";

            string dateFmt = null;
            switch (schedule)
            {
                case LogRotateSchedule.Yearly:
                dateFmt = "yyyy";
                break;
                case LogRotateSchedule.Monthly:
                dateFmt = "yyyy-MM";
                break;
                case LogRotateSchedule.Daily:
                dateFmt = "yyyy-MM-dd";
                break;
            }

            if (dateFmt != null)
                return path + seperator + date.ToString(dateFmt) + extension;
            else
                return path + extension;
        }
    }
}