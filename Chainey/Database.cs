using System;
using Mono.Data.Sqlite;
using System.Collections.Generic;

namespace Chainey
{
    public class SqliteBack
    {
        public int Order { get; private set; }

        volatile int _maxWords;
        public int MaxWords
        {
            get { return _maxWords; }
            set
            {
                if (value > 0)
                    _maxWords = value;
                else
                    throw new ArgumentOutOfRangeException("value", "MaxWords cannot be less than or equal to 0.");
            }
        }

        readonly string connStr;
        private enum Direction
        {
            Forward,
            Backward
        }


        public SqliteBack(string path, int order)
        {
            connStr = "URI=file:" + path;
            Order = order;
            MaxWords = 100;

            using (var connection = new SqliteConnection(connStr))
            {
                connection.Open();

                using (var cmd = new SqliteCommand(connection))
                {
                    // ----- Populate the database with tables, if necessary -----
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS WordCount(" +
                        "word TEXT NOT NULL, count INTEGER NOT NULL DEFAULT 1, UNIQUE(word))";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS Forward(" +
                        "chain TEXT NOT NULL, followup TEXT NOT NULL DEFAULT '', UNIQUE(chain, followup))";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS Backward(" +
                        "chain TEXT NOT NULL, followup TEXT NOT NULL DEFAULT '', UNIQUE(chain, followup))";
                    cmd.ExecuteNonQuery();
                }

                connection.Close();
            }
        }


        // -----
        // Methods for adding sentences.
        // -----

        public void AddSentence(string sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            string[] words = sentence.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            AddSentence(words);
        }

        public void AddSentence(string[] sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");
            // Return early if there's nothing to do.
            else if (sentence.Length < Order)
                return;

            string[] reversed = ReverseCopy(sentence);
            
            string[][] forwardChains = MarkovTools.TokenizeSentence(sentence, Order);
            string[][] backwardChains = MarkovTools.TokenizeSentence(reversed, Order);
            
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (SqliteTransaction tr = conn.BeginTransaction())
                using (SqliteCommand insertCmd = conn.CreateCommand())
                {
                    insertCmd.Transaction = tr;

                    UpdateWordCount(sentence, insertCmd);

                    InsertChains(forwardChains, Direction.Forward, insertCmd);
                    InsertChains(backwardChains, Direction.Backward, insertCmd);

                    tr.Commit();
                }
                conn.Close();
            }
        }

        static string[] ReverseCopy(string[] arr)
        {
            var reversed = new string[arr.Length];

            int j = arr.Length - 1;
            for (int i = 0; i < arr.Length; i++, j--)
                reversed[i] = arr[j];

            return reversed;
        }


        static void InsertChains(string[][] chains, Direction dir, SqliteCommand insertCmd)
        {
            const string cmd = "INSERT INTO {0} (chain, followup) SELECT @Chain, @FollowUp " +
                "WHERE NOT EXISTS(SELECT 1 FROM {0} WHERE chain=@Chain AND followup=@FollowUp)";
            insertCmd.CommandText = FormatSql(cmd, dir);
            insertCmd.Prepare();

            string insertChain, insertFollow;
            foreach (string[] chain in chains)
            {
                insertChain = string.Join(" ", chain, 0, chain.Length - 1);
                insertFollow = chain[chain.Length - 1] ?? string.Empty;

                insertCmd.Parameters.AddWithValue("@Chain", insertChain);
                insertCmd.Parameters.AddWithValue("@FollowUp", insertFollow);
                insertCmd.ExecuteNonQuery();
            }
        }


        // -----
        // Methods having to do with WordCount.
        // -----

        static void UpdateWordCount(string[] sentence, SqliteCommand cmd)
        {
            const string countSql = "INSERT OR REPLACE INTO WordCount (word, count) " +
                "VALUES( @Word, COALESCE( (SELECT count + 1 FROM WordCount WHERE word=@Word), 1 ) )";
            cmd.CommandText = countSql;
            cmd.Prepare();
            
            foreach (string word in sentence)
            {
                cmd.Parameters.AddWithValue("@Word", word.ToUpperInvariant());
                cmd.ExecuteNonQuery();
            }
        }


        // Modifies `words` in place.
        public void SortByWordCount(string[] words)
        {
            if (words == null)
                throw new ArgumentNullException("words");
            // Return early if there's nothing to sort.
            else if (words.Length <= 1)
                return;

            var counts = new ulong[words.Length];
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    int i = 0;
                    foreach (string word in words)
                    {
                        counts[i] = WordCount(word, cmd);
                        i++;
                    }
                }
                conn.Close();
            }

            Array.Sort(counts, words);
        }


        public void SortByRarity(string[] sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");
            // Return early if there's nothing to sort.
            else if (sentences.Length <= 1)
                return;

            var rarities = new double[sentences.Length];
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    int i = 0;
                    foreach (string sen in sentences)
                    {
                        rarities[i] = SentenceRarity(sen, cmd);
                        i++;
                    }
                }
                conn.Close();
            }

            Array.Sort(rarities, sentences);
        }


        static double SentenceRarity(string sentence, SqliteCommand cmd)
        {
            ulong sum = 0;
            foreach ( string word in sentence.Split(' ') )
                sum += WordCount(word, cmd);

            return sum / (double)sentence.Length;
        }


        // Return as ulong (unsigned 64-bit integer) even though SQLite stores it as signed 64-bit integers because it
        // allows easy usage for summing/addition. Count will also never be negative, so there's no drawbacks.
        static ulong WordCount(string word, SqliteCommand cmd)
        {
            const string sqlCmd = "SELECT count FROM WordCount WHERE word=@Word";
            if (cmd.CommandText != sqlCmd)
            {
                cmd.CommandText = sqlCmd;
                cmd.Prepare();
            }
            cmd.Parameters.AddWithValue("@Word", word.ToUpperInvariant());

            // If word is not found in the WordCount table, return 0.
            var count = cmd.ExecuteScalar() as ulong?;
            return count ?? 0;
        }


        // -----
        // Methods for building sentences.
        // -----

        // Returns an array of sentences equal or less of the size of the seeds array passed in. It can return an empty
        // array, in case none of the seeds resulted in a sentence.
        public IEnumerable<string> BuildSentences(IEnumerable<string> seeds)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            using (var connection = new SqliteConnection(connStr))
            {
                connection.Open();
                using (var cmd = new SqliteCommand(connection))
                {
                    string sentence;
                    foreach (string seed in seeds)
                    {
                        if (string.IsNullOrWhiteSpace(seed))
                            continue;

                        CreateSeedSql(seed, cmd);
                        sentence = BuildASentence(cmd);
                        if (sentence != null)
                            yield return sentence;
                    }
                }
                connection.Close();
            }
        }


        public string BuildRandomSentence()
        {
            return BuildSentence(null);
        }

        // If seed is null it will build a sentence starting with a randomly selected chain.
        // This will return null in case no sentence could be constructed.
        public string BuildSentence(string seed)
        {
            const string randomChainSql = "SELECT * FROM Forward ORDER BY RANDOM() LIMIT 1";

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

                    sentence = BuildASentence(cmd);
                }
                connection.Close();
            }

            return sentence;
        }


        // This method assumes you've already set the CommandText in the calling method.
        string BuildASentence(SqliteCommand cmd)
        {
            string chain, followUp;
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    chain = reader.GetString(0);
                    followUp = reader.GetString(1);
                }
                else
                    return null;
            }

            // Backward search.
            List<string> words = BackSearch(chain, cmd);
            // Forward search.
            if (!string.IsNullOrEmpty(followUp))
            {
                words.Add(followUp);
                CollectChains(words, Direction.Forward, cmd);
            }

            return string.Join(" ", words);
        }

        // Put this into its own method, because of the Reverse stuff.
        List<string> BackSearch(string start, SqliteCommand cmd)
        {
            var results = new List<string>( start.Split(' ') );
            results.Reverse();

            CollectChains(results, Direction.Backward, cmd);

            results.Reverse();
            return results;
        }


        void CollectChains(List<string> coll, Direction dir, SqliteCommand cmd)
        {
            const string searchSql = "SELECT followup FROM {0} WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
            cmd.CommandText = FormatSql(searchSql, dir);
            cmd.Prepare();

            string followUp, chain;
            while (coll.Count <= MaxWords)
            {
                chain = GetLatestChain(coll);
                cmd.Parameters.AddWithValue("@Chain", chain);
                
                followUp = cmd.ExecuteScalar() as string;
                if ( !string.IsNullOrEmpty(followUp) )
                    coll.Add(followUp);
                else
                    return;
            }
        }

        string GetLatestChain(List<string> sentence)
        {
            var chain = new string[Order];
            
            int j = (sentence.Count - Order);
            for (int i = 0; i < Order; i++, j++)
                chain[i] = sentence[j];
            
            return string.Join(" ", chain);
        }


        // -----
        // Methods for constructing SQL statements.
        // -----

        static string FormatSql(string sql, Direction dir)
        {
            switch (dir)
            {
            case Direction.Forward:
                return string.Format(sql, "Forward");
            case Direction.Backward:
                return string.Format(sql, "Backward");
            default:
                throw new InvalidOperationException("Unexpected Direction.");
            }
        }


        static void CreateSeedSql(string seed, SqliteCommand cmd)
        {
            const string seedSql = "SELECT * FROM Forward WHERE chain LIKE @SeedPat OR followup=@Seed " +
                "ORDER BY RANDOM() LIMIT 1";
            if (cmd.CommandText != seedSql)
            {
                /* cmd.CommandText = "PRAGMA case_sensitive_like=ON";
                cmd.ExecuteNonQuery(); */
                cmd.CommandText = seedSql;
                cmd.Prepare();
            }
            var seedPattern = string.Concat(seed, "%");
            cmd.Parameters.AddWithValue("@SeedPat", seedPattern);
            cmd.Parameters.AddWithValue("@Seed", seed);
        }
    }
}