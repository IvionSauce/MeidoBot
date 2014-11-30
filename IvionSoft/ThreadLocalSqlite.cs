using System;
using System.Timers;
using System.Threading;
using Mono.Data.Sqlite;


namespace IvionSoft
{
    public class ThreadLocalSqlite : IDisposable
    {
        ThreadLocal<LocalSqlite> local;


        public ThreadLocalSqlite(string location) : this(location, TimeSpan.FromMinutes(10)) {}

        public ThreadLocalSqlite(string location, TimeSpan connectionTimeout)
        {
            location.ThrowIfNullOrWhiteSpace("location");

            var connStr = "URI=file:"+location+";Journal Mode=WAL";
            local = new ThreadLocal<LocalSqlite>
                ( () => new LocalSqlite(connStr, connectionTimeout) );
        }


        public SqliteConnection GetDb()
        {
            return local.Value.Connection;
        }


        public void Dispose()
        {
            local.Dispose();
        }
    }


    internal class LocalSqlite : IDisposable
    {
        readonly string connStr;
        readonly uint timeout;

        readonly System.Timers.Timer cleaner = new System.Timers.Timer();

        object _locker = new object();

        SqliteConnection conn;
        uint accessTime;
        internal SqliteConnection Connection
        {
            get
            {
                lock (_locker)
                {
                    accessTime = (uint)Environment.TickCount;

                    if (conn == null)
                    {
                        conn = new SqliteConnection(connStr);
                        conn.Open();

                        cleaner.Interval = timeout;
                        cleaner.Start();
                    }

                    return conn;
                }
            }
        }


        internal LocalSqlite(string connStr, TimeSpan connTimeout)
        {
            this.connStr = connStr;
            timeout = (uint)connTimeout.TotalMilliseconds;

            cleaner.AutoReset = false;
            cleaner.Elapsed += new ElapsedEventHandler(Cleaner);
        }


        void Cleaner(object sender, ElapsedEventArgs e)
        {
            lock (_locker)
            {
                var timeSinceLastAccess = (uint)Environment.TickCount - accessTime;

                if (timeSinceLastAccess >= timeout)
                {
                    conn.Dispose();
                    conn = null;
                }
                else
                {
                    // If the database hasn't been accessed for half the timeout or longer, schedule the next Cleaner
                    // for the projected time remaining.
                    if (timeSinceLastAccess >= timeout / 2)
                        cleaner.Interval = timeout - timeSinceLastAccess;

                    // Else keep the standard interval.
                    else if (cleaner.Interval != timeout)
                        cleaner.Interval = timeout;

                    cleaner.Start();
                }
            } // lock
        }


        public void Dispose()
        {
            cleaner.Dispose();
            conn.Dispose();
        }
    }
}

