using System;
using System.Threading;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class AutoReconnect
    {
        public bool Enabled { get; set; }


        readonly static TimeSpan minInterval = TimeSpan.FromSeconds(15);
        readonly static TimeSpan maxInterval = TimeSpan.FromMinutes(32);
        TimeSpan currentInterval = minInterval;

        readonly IrcClient irc;
        string[] serverAddresses;
        int serverPort;


        public AutoReconnect(IrcClient irc)
        {
            irc.OnConnectionError += OnConnectionError;
            this.irc = irc;
        }


        public void SuccessfulConnect()
        {
            currentInterval = minInterval;

            serverAddresses = new string[irc.AddressList.Length];
            irc.AddressList.CopyTo(serverAddresses, 0);
            serverPort = irc.Port;

            Enabled = true;
        }

        void OnConnectionError(object sender, EventArgs e)
        {
            try
            {
                irc.Disconnect();
            }
            catch (NotConnectedException)
            {
                // Well, we're disconnected. Which is the state we want to be in.
            }
            EndlessReconnect();
        }

        void EndlessReconnect()
        {
            while (Enabled && !irc.IsConnected)
            {
                var reconnectInterval = currentInterval;
                currentInterval = NewInterval(currentInterval);

                Thread.Sleep(reconnectInterval);
                try
                {
                    irc.Connect(serverAddresses, serverPort);
                }
                catch (CouldNotConnectException)
                {
                    // We'll keep retrying with an increasing interval.
                }
            }
        }

        static TimeSpan NewInterval(TimeSpan interval)
        {
            if (interval < maxInterval)
            {
                var newInterval = TimeSpan.FromSeconds(interval.TotalSeconds * 2);

                if (newInterval <= maxInterval)
                    return newInterval;
            }

            return maxInterval;
        }
    }
}