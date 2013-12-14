using System.Collections.Generic;
using System.Threading;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcVoting : IMeidoHook
{
    VotingManager votingManager;

    public string Description
    {
        get { return "IrcVoting v0.22"; }
    }

    public Dictionary<string,string> exportedHelp
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {".vote", ".vote <motion> - Starts vote on a motion, there can only be one vote ongoing at a time."}
            };
        }
    }


    [ImportingConstructor]
    public IrcVoting(IIrcComm ircComm)
    {
        ircComm.AddChannelMessageHandler(HandleChannelMessage);
        votingManager = new VotingManager(ircComm);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        switch(e.MessageArray[0])
        {
        case ".y":
            new Thread( () => votingManager.AddYay(e.Channel, e.Nick, e.Host) ).Start();
            break;
        case ".n":
            new Thread( () => votingManager.AddNay(e.Channel, e.Nick, e.Host) ).Start();
            break;
        case ".vote":
            if (e.MessageArray.Length > 1)
            {
                // Index 0 is ".vote" and everything from index 1 onward is the `motion`.
                var motion = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
                votingManager.StartVoting(e.Channel, motion);
            }
            break;
        }
    }
}


class VotingManager
{
    IIrcComm irc;

    Dictionary<string, VoteStorage> storageIndex = new Dictionary<string, VoteStorage>();
    object _locker = new object();


    // Constructor
    public VotingManager(IIrcComm irc)
    {
        this.irc = irc;
    }

    public void AddYay(string channel, string nick, string host)
    {
        if (VotingInProgess(channel))
        {
            if (HasAlreadyVoted(channel, host))
            {
                irc.SendNotice(nick, "You have already voted.");
                return;
            }

            lock (_locker)
                storageIndex[channel].Yay.Add(host);
            irc.SendNotice(nick, "Your vote has been registered.");
        }
    }

    public void AddNay(string channel, string nick, string host)
    {
        if (VotingInProgess(channel))
        {
            if (HasAlreadyVoted(channel, host))
            {
                irc.SendNotice(nick, "You have already voted.");
                return;
            }

            lock (_locker)
                storageIndex[channel].Nay.Add(host);
            irc.SendNotice(nick, "Your vote has been registered.");
        }
    }

    bool VotingInProgess(string channel)
    {
        lock (_locker)
        {
            VoteStorage channelVote;
            if (storageIndex.TryGetValue(channel, out channelVote))
                return channelVote.VotingOpen;
            else
            {
                storageIndex.Add(channel, new VoteStorage());
                return false;
            }
        }
    }

    bool HasAlreadyVoted(string channel, string host)
    {
        lock (_locker)
        {
            VoteStorage channelVote = storageIndex[channel];
            if (channelVote.Yay.Contains(host) || channelVote.Nay.Contains(host))
                return true;
        }
        return false;
    }

    public void StartVoting(string channel, string motion)
    {
        if (VotingInProgess(channel))
            return;
        else
        {
            lock (_locker)
            {
                VoteStorage channelVote = storageIndex[channel];

                channelVote.Yay = new List<string>();
                channelVote.Nay = new List<string>();
                channelVote.VotingOpen = true;
            }

            irc.SendMessage(channel, "Starting vote on motion: " + motion);
            new Thread( () => Voting(channel) ).Start();
        }
    }

    void Voting(string channel)
    {
        // The reason I wait for 21 and 11 seconds is because of the inherent slowness in any network,
        // whether it be lag of the network equipment or client-side lag.
        irc.SendMessage(channel, @"Voting open for 30 seconds. Vote ""Yay"" or ""Nay"" with .y/.n");
        // Wait for 21 seconds.
        Thread.Sleep(21000);
        irc.SendMessage(channel, "Ten seconds remaining...");
        // Wait for 11 seconds.
        Thread.Sleep(11000);
        irc.SendMessage(channel, "Voting closed.");

        int yay, nay;
        lock (_locker)
        {
            VoteStorage channelVote = storageIndex[channel];

            channelVote.VotingOpen = false;
            yay = channelVote.Yay.Count;
            nay = channelVote.Nay.Count;
        }

        string motionStatus;
        if (yay > nay)
            motionStatus = "Motion is accepted!";
        else if (yay < nay)
            motionStatus = "Motion is rejected.";
        else
            motionStatus = "Hung vote!";

        // Wait for 1 second, else it follows too quickly after the previous message.
        Thread.Sleep(1000);
        var results = string.Format("Voting results - Yay: {0}, Nay: {1}. {2}", yay, nay, motionStatus);

        irc.SendMessage(channel, results);
    }
}


class VoteStorage
{
    public bool VotingOpen { get; set; }
    public List<string> Yay { get; set; }
    public List<string> Nay { get; set; }


    // Constructor
    public VoteStorage()
    {
        VotingOpen = false;
    }
}