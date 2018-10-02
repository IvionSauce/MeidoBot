using System;


namespace MeidoBot
{
    class UserAuth
    {
        public readonly int Level;

        readonly string password;

        bool authenticated;
        DateTimeOffset authTime;
        static readonly TimeSpan maxTime = TimeSpan.FromMinutes(10);
        object _authLock = new object();

        public bool IsAuthenticated
        {
            get
            {
                lock (_authLock)
                {
                    if (DateTimeOffset.Now - authTime < maxTime)
                        return authenticated;
                    else
                        return false;
                }
            }
        }


        public UserAuth(string pass, int lvl)
        {
            if (string.IsNullOrWhiteSpace(pass))
                password = string.Empty;
            else
                password = pass;

            const int maxLvl = 3;
            if (lvl > maxLvl)
                Level = maxLvl;
            else if (lvl < 0)
                Level = 0;
            else
                Level = lvl;
        }


        public bool Authenticate(string pass)
        {
            if (password.Equals(pass, StringComparison.Ordinal))
            {
                lock (_authLock)
                {
                    authenticated = true;
                    authTime = DateTimeOffset.Now;
                    return true;
                }
            }
            else
                return false;
        }

    }
}