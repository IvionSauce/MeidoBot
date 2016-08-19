namespace MeidoBot
{
    interface IChatlogger
    {
        void Message(string target, string message);
        void Action(string target, string message);
        void Notice(string target, string message);
    }


    // Implement as no-ops.
    class DummyChatlogger : IChatlogger
    {
        public void Message(string target, string message)
        {
            ;
        }

        public void Action(string target, string message)
        {
            ;
        }

        public void Notice(string target, string message)
        {
            ;
        }
    }
}