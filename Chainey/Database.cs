using System;
using Mono.Data.Sqlite;
using System.Collections.Generic;

namespace Chainey
{
    public class SqliteBack
    {
        readonly string connStr;

        public int Order { get; private set; }


        public SqliteBack(string path, int order)
        {
            connStr = "URI=file:" + path;
            Order = order;

            using (var connection = new SqliteConnection(connStr))
            {
                connection.Open();

                using (var cmd = new SqliteCommand(connection))
                {
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS Chainey(chain TEXT NOT NULL, followup TEXT)";
                    cmd.ExecuteNonQuery();
                }

                connection.Close();
            }
        }

        public void AddChains(string[][] chainCollection)
        {
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (SqliteTransaction tr = conn.BeginTransaction())
                using (SqliteCommand insertCmd = conn.CreateCommand(),
                                     checkCmd = conn.CreateCommand())
                {
                    insertCmd.Transaction = tr;
                    checkCmd.Transaction = tr;
                    InsertChains(chainCollection, insertCmd, checkCmd);
                    tr.Commit();
                }
                conn.Close();
            }
        }

        static void InsertChains(string[][] chains, SqliteCommand insertCmd, SqliteCommand checkCmd)
        {
            insertCmd.CommandText = "INSERT INTO Chainey VALUES(@Chain, @FollowUp)";
            insertCmd.Prepare();
            // Use `IS` for the follow-up, since it can be null. Checking with `=` always returns false when checking if
            // something is null.
            checkCmd.CommandText = "SELECT EXISTS(SELECT 1 FROM Chainey WHERE chain=@Chain " +
                "AND followup IS @FollowUp LIMIT 1)";
            checkCmd.Prepare();

            foreach (string[] chain in chains)
            {
                var insertChain = string.Join(" ", chain, 0, chain.Length - 1);
                var insertFollow = chain[chain.Length - 1];

                // Only add if it doesn't already exist.
                if (!CheckIfExists(insertChain, insertFollow, checkCmd))
                {
                    insertCmd.Parameters.AddWithValue("@Chain", insertChain);
                    insertCmd.Parameters.AddWithValue("@FollowUp", insertFollow);
                    insertCmd.ExecuteNonQuery();
                }
            }
        }

        static bool CheckIfExists(string chain, string followUp, SqliteCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Chain", chain);
            cmd.Parameters.AddWithValue("@FollowUp", followUp);

            var value = Convert.ToInt32(cmd.ExecuteScalar());
            if (value == 1)
                return true;
            else
                return false;
        }

        // Returns an array of sentences equal or less of the size of the seeds array passed in. It can return an empty
        // array, in case none of the seeds resulted in a sentence.
        public string[] BuildSentences(string[] seeds, int maxWords)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            var sentences = new List<string>(seeds.Length);

            using (var connection = new SqliteConnection(connStr))
            {
                connection.Open();
                using (var cmd = new SqliteCommand(connection))
                {
                    string tmpSentence;
                    foreach (string seed in seeds)
                    {
                        if (string.IsNullOrWhiteSpace(seed))
                            continue;

                        CreateSeedSql(seed, cmd);
                        tmpSentence = BuildASentence(maxWords, cmd);

                        if (tmpSentence != null)
                            sentences.Add(tmpSentence);
                    }
                }
                connection.Close();
            }
            return sentences.ToArray();
        }

        public string BuildRandomSentence(int maxWords)
        {
            return BuildSentence(null, maxWords);
        }

        // If seed is null it will build a sentence starting with a randomly selected chain.
        // This will return null in case no sentence could be constructed.
        public string BuildSentence(string seed, int maxWords)
        {
            const string randomChainSql = "SELECT * FROM Chainey ORDER BY RANDOM() LIMIT 1";

            string sentence;
            using (var connection = new SqliteConnection(connStr))
            {
                connection.Open();
                using (var cmd = new SqliteCommand(connection))
                {
                    if (seed == null)
                        cmd.CommandText = randomChainSql;
                    else
                        CreateSeedSql(seed, cmd);

                    sentence = BuildASentence(maxWords, cmd);
                }
                connection.Close();
            }

            return sentence;
        }

        // This method assumes you've already set the CommandText in the calling method.
        string BuildASentence(int maxWords, SqliteCommand cmd)
        {
            string chain = null;
            string followUp = null;
            // Need to finish using the reader before the SqliteCommand is free for other commands (GetFollowUp).
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    chain = reader.GetString(0);
                    // Leave at null if follow-up is null in DB.
                    if (!reader.IsDBNull(1))
                        followUp = reader.GetString(1);
                }
            }
            // In case we didn't get a result.
            if (chain == null)
                return null;

            // Storage for the sentence-to-build.
            var words = new List<string>();
            words.AddRange( chain.Split(' ') );

            // Only limit the word-count if maxWords is set to a sensible value.
            bool limitWords = maxWords > 0;
            // If the initial chain doesn't have a follow-up this loop will never be entered.
            while (followUp != null)
            {
                words.Add(followUp);
                if (limitWords && words.Count >= maxWords)
                    break;

                followUp = GetFollowUp(words, cmd);
            }

            return string.Join(" ", words);
        }

        static void CreateSeedSql(string seed, SqliteCommand cmd)
        {
            const string seedSql = "SELECT * FROM Chainey WHERE chain LIKE @SeedPat OR followup=@Seed " +
                "ORDER BY RANDOM() LIMIT 1";
            if (cmd.CommandText != seedSql)
            {
                cmd.CommandText = "PRAGMA case_sensitive_like=ON";
                cmd.ExecuteNonQuery();
                cmd.CommandText = seedSql;
                cmd.Prepare();
            }
            var seedPattern = string.Concat("%", seed, "%");
            cmd.Parameters.AddWithValue("@SeedPat", seedPattern);
            cmd.Parameters.AddWithValue("@Seed", seed);
        }

        // Will return null if follow-up is null or if the chain doesn't exist in the database.
        string GetFollowUp(List<string> sentence, SqliteCommand cmd)
        {
            const string followUpSql = "SELECT followup FROM Chainey WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
            if (cmd.CommandText != followUpSql)
            {
                cmd.CommandText = followUpSql;
                cmd.Prepare();
            }
            string chain = MarkovTools.GetLatestChain(sentence, Order);
            cmd.Parameters.AddWithValue("@Chain", chain);

            var followUp = cmd.ExecuteScalar() as string;
            return followUp;
        }
    }
}