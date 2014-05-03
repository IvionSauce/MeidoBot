using System;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using IvionSoft;

namespace Chainey
{
    public class SqliteBack
    {
        public int Order { get; private set; }

        volatile int _maxWords;
        /// <summary>
        /// Gets or sets the maximum word count. This is used as a limit when building sentences, as a safeguard against
        /// an infinite search. Thread safe, but if set when building a sentence resulting sentence will be of previous
        /// value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if MaxWords is set to 0 or lower.</exception>
        /// <value>Maximum word count. Default: 100.</value>
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


        // ***
        // -----------------------------
        // Methods for adding sentences.
        // -----------------------------
        // ***


        /// <summary>
        /// Adds sentence to the brain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if sentence is null.</exception>
        /// <param name="sentence">Sentence.</param>
        public void AddSentence(string sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            string[] words = sentence.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
            InternalAdd(words);
        }

        /// <summary>
        /// Adds sentence to the brain. Null, empty and whitespace strings are filtered from sentence.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if sentence is null.</exception>
        /// <param name="sentence">Sentence (array of words).</param>
        public void AddSentence(string[] sentence)
        {
            AddSentence(sentence, true);
        }

        /// <summary>
        /// Adds sentence to the brain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if sentence is null.</exception>
        /// <param name="sentence">Sentence (array of words).</param>
        /// <param name="filter">If set to <c>true</c> check input sentence for null, empty and whitespace strings and
        /// remove those before adding.</param>
        public void AddSentence(string[] sentence, bool filter)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            // Make sure the array doesn't contain any null, empty or whitespace items. Also trim before adding.
            // Can be disabled by those who know for sure their array doesn't contain any and want to avoid this cost.
            if (filter)
            {
                string trimmed;
                var cleaned = new List<string>(sentence.Length);
                for (int i = 0; i < sentence.Length; i++)
                {
                    if (sentence[i] != null)
                    {
                        trimmed = sentence[i].Trim();
                        if (trimmed != string.Empty)
                            cleaned.Add(trimmed);
                    }
                }

                InternalAdd( cleaned.ToArray() );
            }
            else
                InternalAdd(sentence);
        }

        void InternalAdd(string[] sentence)
        {
            // Return early if there's nothing to do.
            if (sentence.Length < Order)
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
            // const string cmd = "INSERT INTO {0} VALUES(@Chain, @FollowUp) ON CONFLICT IGNORE";
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


        // ***
        // ------------------------------------
        // Methods having to do with WordCount.
        // ------------------------------------
        // ***

        // Increment word count by 1 if it has an entry, else create entry with count 1.
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


        /// <summary>
        /// Sorts words by their word count, from less to more common.
        /// Words that are null, empty or whitespace will be treated as most common.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if words is null.</exception>
        /// <param name="words">Words.</param>
        public void SortByWordCount(string[] words)
        {
            if (words == null)
                throw new ArgumentNullException("words");
            // Return early if there's nothing to sort.
            else if (words.Length <= 1)
                return;

            var counts = new long[words.Length];
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    int i = 0;
                    foreach (string word in words)
                    {
                        if (!string.IsNullOrWhiteSpace(word))
                            counts[i] = WordCount(word, cmd);
                        else
                            counts[i] = long.MaxValue;
                        i++;
                    }
                }
                conn.Close();
            }

            Array.Sort(counts, words);
        }


        /// <summary>
        /// Sorts sentences by rarity, from less to more rare.
        /// Sentences that are null or have no words (empty or whitespace) will be treated as least rare.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if sentences is null.</exception>
        /// <param name="sentences">Sentences.</param>
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


        /// <summary>
        /// Get the word count.
        /// </summary>
        /// <returns>The word count.</returns>
        /// <exception cref="ArgumentNullException">Thrown if word is null.</exception>
        /// <exception cref="ArgumentException">Thrown if word is empty or whitespace.</exception>
        /// <param name="word">Word.</param>
        public long WordCount(string word)
        {
            word.ThrowIfNullOrWhiteSpace("word");

            long count;
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    count = WordCount(word, cmd);
                }
                conn.Close();
            }

            return count;
        }

        /// <summary>
        /// Get the word counts. Null, empty or whitespace "words" will be skipped.
        /// </summary>
        /// <returns>The word count.</returns>
        /// <exception cref="ArgumentNullException">Thrown if words is null.</exception>
        /// <param name="words">Words.</param>
        public IEnumerable<long> WordCount(IEnumerable<string> words)
        {
            if (words == null)
                throw new ArgumentNullException("words");

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    foreach (string word in words)
                    {
                        if (!string.IsNullOrWhiteSpace(word))
                            yield return (long)WordCount(word, cmd);
                    }
                }
                conn.Close();
            }
        }


        /// <summary>
        /// Get the rarity of a sentence. The further away (positive) from 0, the rarer.
        /// A sentence with no words (empty or whitespace) will have a rarity of Negative Infinity.
        /// </summary>
        /// <returns>The rarity.</returns>
        /// <exception cref="ArgumentNullException">Thrown if sentence is null.</exception>
        /// <param name="sentence">Sentence.</param>
        public double SentenceRarity(string sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            double rarity;
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    rarity =  SentenceRarity(sentence, cmd);
                }
                conn.Close();
            }

            return rarity;
        }

        /// <summary>
        /// Get the rarity of sentences. The further away (positive) from 0, the rarer.
        /// A sentence with no words (empty or whitespace) will have a rarity of Negative Infinity. Null will beget NaN.
        /// </summary>
        /// <returns>The rarity.</returns>
        /// <exception cref="ArgumentNullException">Thrown if sentences is null.</exception>
        /// <param name="sentences">Sentences.</param>
        public IEnumerable<double> SentenceRarity(IEnumerable<string> sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    foreach (string sen in sentences)
                        yield return SentenceRarity(sen, cmd);
                }
                conn.Close();
            }
        }


        // The closer to 0, the less rare the sentence is. If the sentence contains only words we've never seen before
        // the rarity will be `Infinity`.
        // Will return `-Infinity` if the sentence has no words.
        // Will return `NaN` if the sentence is null.
        // If sorted order will be: NaN, -Infinity, [...], Infinity
        static double SentenceRarity(string sentence, SqliteCommand cmd)
        {
            if (sentence == null)
                return double.NaN;

            var split = sentence.Split();

            // Sum word counts in ulong for extra headroom.
            ulong sum = 0;
            int len = split.Length;
            foreach (string word in split)
            {
                if (word != string.Empty)
                    sum += (ulong)WordCount(word, cmd);
                else
                    len--;
            }

            if (len > 0)
                return (double)len / sum;
            else
                return double.NegativeInfinity;
        }


        static long WordCount(string word, SqliteCommand cmd)
        {
            const string sqlCmd = "SELECT count FROM WordCount WHERE word=@Word";
            if (cmd.CommandText != sqlCmd)
            {
                cmd.CommandText = sqlCmd;
                cmd.Prepare();
            }
            cmd.Parameters.AddWithValue("@Word", word.ToUpperInvariant());

            // If word is not found in the WordCount table, return 0.
            var count = cmd.ExecuteScalar() as long?;
            return count ?? 0;
        }


        // ***
        // -------------------------------
        // Methods for building sentences.
        // -------------------------------
        // ***


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


        /// <summary>
        /// Builds a random sentence.
        /// </summary>
        /// <returns>The sentence.</returns>
        public string BuildRandomSentence()
        {
            return BuildSentence(null);
        }

        /// <summary>
        /// Builds a sentence. If seed is null builds a random sentence.
        /// </summary>
        /// <returns>The sentence, or null if no sentence could be build.</returns>
        /// <param name="seed">Seed.</param>
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

            // Populate the sentence-to-be with the initial chain and follow up.
            var words = new List<string>( chain.Split(' ') );
            if (followUp != string.Empty)
                words.Add(followUp);

            // Copy out the volatile field, so we have a consistent MaxWords while building the sentence.
            int maxWords = MaxWords;
            // Backward search.
            words.Reverse();
            CollectChains(words, Direction.Backward, maxWords, cmd);
            words.Reverse();
            // Forward search.
            CollectChains(words, Direction.Forward, maxWords, cmd);

            return string.Join(" ", words);
        }


        void CollectChains(List<string> coll, Direction dir, int maxWords, SqliteCommand cmd)
        {
            const string searchSql = "SELECT followup FROM {0} WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
            cmd.CommandText = FormatSql(searchSql, dir);
            cmd.Prepare();

            string followUp, chain;
            while (coll.Count <= maxWords)
            {
                chain = GetLatestChain(coll);
                cmd.Parameters.AddWithValue("@Chain", chain);
                
                followUp = cmd.ExecuteScalar() as string;
                // If the chain couldn't be found (followUp is null) or if the chain is an ending chain (followUp is
                // empty) stop collecting chains.
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


        // ***
        // ----------------------------------------
        // Methods for constructing SQL statements.
        // ----------------------------------------
        // ***

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