using System;
using System.Threading;
using Mono.Data.Sqlite;


namespace IvionSoft
{
    public class ThreadLocalSqlite : IDisposable
    {
        public TimeSpan ConnectionTimeout { get; set; }

        string connStr;
        ThreadLocal<LocalSqlite> local;


        public ThreadLocalSqlite(string location)
        {
            location.ThrowIfNullOrWhiteSpace("location");

            local = new ThreadLocal<LocalSqlite>
                ( () => new LocalSqlite(connStr, ConnectionTimeout) );

            connStr = string.Concat("URI=file:", location, ";Journal Mode=WAL");
            ConnectionTimeout = TimeSpan.FromMinutes(60);
        }


        public SqliteConnection GetDb()
        {
            return local.Value.Connection;
        }


        public void Dispose()
        {
            local.Value.Dispose();
            local.Dispose();
        }
    }


    internal class LocalSqlite : IDisposable
    {
        string connStr;
        uint timeout;

        object _locker = new object();

        Timer cleaner;

        SqliteConnection conn;
        uint accessTime;
        internal SqliteConnection Connection
        {
            get
            {
                lock (_locker)
                {
                    if (conn == null)
                    {
                        conn = new SqliteConnection(connStr);
                        conn.Open();
                    }

                    accessTime = (uint)Environment.TickCount;
                    return conn;
                }
            }
        }


        internal LocalSqlite(string connStr, TimeSpan connTimeout)
        {
            this.connStr = connStr;
            timeout = (uint)connTimeout.TotalMilliseconds;

            var checkInterval = timeout / 2;
            cleaner = new Timer(Cleaner, null, checkInterval, checkInterval);
        }


        void Cleaner(object state)
        {
            lock (_locker)
            {
                if ( ((uint)Environment.TickCount - accessTime) > timeout )
                {
                    conn.Dispose();
                    conn = null;
                }
            }
        }


        public void Dispose()
        {
            cleaner.Dispose();
            conn.Dispose();
        }
    }
}

