using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;


namespace MeidoBot
{
    class LogWriter : IDisposable
    {
        readonly Queue<LogEntry> queue = new Queue<LogEntry>();
        readonly object _locker = new object();

        readonly Dictionary<string, StreamWriter> pathToWriter =
            new Dictionary<string, StreamWriter>(StringComparer.Ordinal);


        public LogWriter()
        {
            var t = new Thread(Consume);
            t.Start();
            t.Name = "LogWriter";
        }


        public void Enqueue(LogEntry entry)
        {
            lock (_locker)
            {
                queue.Enqueue(entry);
                Monitor.Pulse(_locker);
            }
        }


        void Consume()
        {
            LogEntry entry;
            while (true)
            {
                lock (_locker)
                {
                    while (queue.Count == 0)
                        Monitor.Wait(_locker);

                    entry = queue.Dequeue();
                }

                if (entry != null)
                    ProcessEntry(entry);
                else
                {
                    // Clean up writers before exiting.
                    CloseAll(DateTime.Now);
                    return;
                }
            }
        }


        void ProcessEntry(LogEntry entry)
        {
            // Normal log entry.
            if (entry.LogMessage != null)
                WriteLog(entry);
            // Special log entry, signal to close associated StreamWriter.
            else
                CloseIfOpen(entry.Filepath, entry.Timestamp);
        }


        void WriteLog(LogEntry entry)
        {
            var writer = GetOrOpen(entry.Filepath, entry.Timestamp);
            writer.WriteLine(entry);

            // When there are multiple, consecutive entries destined for the same logfile, try to
            // write as much of them in one go.
            LogEntry next;
            while (NextEntry(entry.Filepath, out next))
            {
                Console.WriteLine("--- Consecutive writes! ({0})", entry.Filepath);
                writer.WriteLine(next);
            }
            writer.Flush();
        }

        bool NextEntry(string filePath, out LogEntry entry)
        {
            lock (_locker)
            {
                if (queue.Count > 0 && queue.Peek().Filepath == filePath)
                {
                    entry = queue.Dequeue();
                    return true;
                }
            }

            entry = null;
            return false;
        }


        StreamWriter GetOrOpen(string filePath, DateTime timestamp)
        {
            StreamWriter writer;
            if (!pathToWriter.TryGetValue(filePath, out writer))
            {
                var stream = File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                writer = new StreamWriter(stream);
                writer.WriteLine("### Opening file on {0:s}", timestamp);

                pathToWriter[filePath] = writer;
            }

            return writer;
        }


        void CloseAll(DateTime timestamp)
        {
            foreach (var w in pathToWriter.Values)
            {
                Close(w, timestamp);
            }
        }

        void CloseIfOpen(string filePath, DateTime timestamp)
        {
            StreamWriter writer;
            if (pathToWriter.TryGetValue(filePath, out writer))
            {
                Close(writer, timestamp);
                pathToWriter.Remove(filePath);
            }
        }

        static void Close(StreamWriter writer, DateTime timestamp)
        {
            writer.WriteLine("### Closing file on {0:s}", timestamp);
            writer.Dispose();
        }


        public void Dispose()
        {
            lock (_locker)
            {
                queue.Enqueue(null);
                Monitor.Pulse(_locker);
            }
        }
    }
}