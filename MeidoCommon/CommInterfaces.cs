using System;

namespace MeidoCommon
{
    public interface IMeidoComm
    {
        /// <summary>
        /// Full path of the configuration directory.
        /// </summary>
        string ConfDir { get; }
        /// <summary>
        /// Full path of the data directory.
        /// </summary>
        string DataDir { get; }

        /// <summary>
        /// Gives full path to configuration file.
        /// </summary>
        /// <returns>Full path to the configuration file.</returns>
        /// <param name="filename">Filename.</param>
        string ConfPathTo(string filename);
        /// <summary>
        /// Gives full path to data file.
        /// </summary>
        /// <returns>Full path to the configuration file.</returns>
        /// <param name="filename">Filename.</param>
        string DataPathTo(string filename);

        /// <summary>
        /// Creates a logger instance for Meidobot plugin.
        /// </summary>
        /// <returns>The logger.</returns>
        /// <param name="plugin">Plugin.</param>
        ILog CreateLogger(IMeidoHook plugin);

        void RegisterTrigger(string trigger, Action<IIrcMessage> callback);
        void RegisterTrigger(string trigger, Action<IIrcMessage> callback, bool needChannel);

        /// <summary>
        /// Loads configuration and watches the file for changes. The function gets called with the full path of the
        /// configuration file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <param name="loadConfig">Function that loads the configuration from passed path.</param>
        void LoadAndWatchConfig(string filename, Action<string> loadConfig);

        /// <summary>
        /// Gives authentication level for nick.
        /// </summary>
        /// <returns>The authentication level.</returns>
        /// <param name="nick">Nick.</param>
        int AuthLevel(string nick);
    }


    public interface IIrcComm
    {
        string Nickname { get; }

        void AddChannelMessageHandler(Action<IIrcMessage> handler);
        void AddChannelActionHandler(Action<IIrcMessage> handler);

        void AddQueryMessageHandler(Action<IIrcMessage> handler);
        void AddQueryActionHandler(Action<IIrcMessage> handler);

        void SendMessage(string target, string message);
        void SendMessage(string target, string message, params object[] args);

        void DoAction(string target, string action);
        void DoAction(string target, string action, params object[] args);

        void SendNotice(string target, string message);
        void SendNotice(string target, string message, params object[] args);

        string[] GetChannels();
        bool IsMe(string nick);
        bool IsJoined(string channel, string nick);
    }
}