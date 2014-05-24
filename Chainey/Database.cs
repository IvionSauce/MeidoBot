using System;
using System.Linq;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using IvionSoft;

namespace Chainey
{
    public class SqliteBrain : IBrainBackend
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


        public SqliteBrain(string file, int order)
        {
            file.ThrowIfNullOrWhiteSpace("file");
            if (order < 1)
                throw new ArgumentOutOfRangeException("order", "Cannot be less than or equal to 0.");

            connStr = string.Concat("URI=file:", file, ";Pooling=true");
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
        /// Adds sentence to the brain. Sentence should not have null or empty entries.
        /// On what you Split (besides spaces) depends on how much whitespace 'formatting' you want the brain to retain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if sentenceWords is null.</exception>
        /// <exception cref="ArgumentException">Thrown if sentenceWords contains null or empty entries.</exception>
        /// <param name="sentenceWords">Sentence (array of words).</param>
        public void AddSentence(string[] sentenceWords)
        {
            if (sentenceWords == null)
                throw new ArgumentNullException("sentenceWords");
            // Return early if there's nothing to do.
            else if (sentenceWords.Length < Order)
                return;
            
            string[] reversed = ReverseCopy(sentenceWords);
            
            string[][] forwardChains = MarkovTools.TokenizeSentence(sentenceWords, Order);
            string[][] backwardChains = MarkovTools.TokenizeSentence(reversed, Order);
            
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (SqliteTransaction tr = conn.BeginTransaction())
                using (SqliteCommand insertCmd = conn.CreateCommand())
                {
                    insertCmd.Transaction = tr;
                    
                    UpdateWordCount(sentenceWords, insertCmd);
                    
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
            const string cmd = "INSERT OR IGNORE INTO {0} VALUES(@Chain, @FollowUp)";
            insertCmd.CommandText = FormatSql(cmd, dir);
            insertCmd.Prepare();

            foreach (string[] chain in chains)
            {
                var insertChain = string.Join(" ", chain, 0, chain.Length - 1);
                var insertFollow = chain[chain.Length - 1] ?? string.Empty;

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
                if (!string.IsNullOrEmpty(word))
                {
                    cmd.Parameters.AddWithValue("@Word", Normalize(word));
                    cmd.ExecuteNonQuery();
                }
                else
                    throw new ArgumentException("Null or empty \"word\" detected.", "sentenceWords");
            }
        }


        /// <summary>
        /// Get the word counts. Null or empty "words" will yield -1.
        /// </summary>
        /// <returns>The word count.</returns>
        /// <exception cref="ArgumentNullException">Thrown if words is null.</exception>
        /// <param name="words">Words.</param>
        public IEnumerable<long> WordCount(IEnumerable<string> words)
        {
            if (words == null)
                throw new ArgumentNullException("words");

            const string countSql = "SELECT count FROM WordCount WHERE word=@Word";

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = countSql;
                    cmd.Prepare();

                    foreach (string word in words)
                        yield return WordCount(word, cmd);
                }
                conn.Close();
            }
        }


        static long WordCount(string word, SqliteCommand cmd)
        {
            if (string.IsNullOrEmpty(word))
                return -1;

            cmd.Parameters.AddWithValue("@Word", Normalize(word));

            // If word is not found in the WordCount table, return 0.
            var count = cmd.ExecuteScalar() as long?;
            return count ?? 0;
        }


        // For when adding or looking up a word. To make sure each word is approached consistently, regardless of the
        // splitting method used.
        // Inspired by http://www.dotnetperls.com/punctuation
        static string Normalize(string word)
        {
            // Count start whitespace and punctuation.
            int removeFromStart = 0;
            for (int i = 0; i < word.Length; i++)
            {
                if (char.IsWhiteSpace(word[i]) || char.IsPunctuation(word[i]))
                    removeFromStart++;
                else
                    break;
            }

            // If only punctuation/whitespace, return as is.
            // This will never be empty, since calling methods guard against that.
            if (removeFromStart == word.Length)
                return word;

            // Count end whitespace and punctuation.
            int removeFromEnd = 0;
            for (int i = (word.Length - 1); i >= 0; i--)
            {
                if (char.IsWhiteSpace(word[i]) || char.IsPunctuation(word[i]))
                    removeFromEnd++;
                else
                    break;
            }

            // If no leading and/or trailing whitespace/punctuation, just uppercase it.
            if (removeFromStart == 0 && removeFromEnd == 0)
                return word.ToUpperInvariant();

            // Remove leading and trailing whitespace/punctuation before uppercasing.
            else
            {
                int len = word.Length - removeFromEnd - removeFromStart;
                string removed = word.Substring(removeFromStart, len);

                return removed.ToUpperInvariant();
            }
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

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (SqliteCommand chainCmd = conn.CreateCommand(),
                       collectCmd = conn.CreateCommand())
                {
                    foreach (string seed in seeds)
                    {
                        if (string.IsNullOrEmpty(seed))
                            continue;

                        CreateSeedSql(seed, chainCmd);

                        foreach (string sentence in Builder(chainCmd, collectCmd))
                            yield return sentence;
                    }
                }
                conn.Close();
            }
        }


        IEnumerable<string> Builder(SqliteCommand chainCmd, SqliteCommand collectCmd)
        {
            using (var reader = chainCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string chain = reader.GetString(0);
                    string followUp = reader.GetString(1);

                    var words = new SentenceConstruct(chain);

                    // Copy out the volatile field, so we have a consistent MaxWords while building the sentence.
                    int maxWords = MaxWords;

                    // Backward search.
                    CollectChains(words, Direction.Backward, maxWords, collectCmd);

                    if (followUp != string.Empty)
                    {
                        // Forward search.
                        words.Append(followUp);
                        CollectChains(words, Direction.Forward, maxWords, collectCmd);
                    }

                    yield return words.Sentence;
                }
            }
        }


        public string BuildRandomSentence()
        {
            const string randomChainSql = "SELECT * FROM Forward ORDER BY RANDOM() LIMIT 1";

            string[] result;
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (SqliteCommand chainCmd = conn.CreateCommand(),
                       collectCmd = conn.CreateCommand())
                {
                    chainCmd.CommandText = randomChainSql;
                    result = Builder(chainCmd, collectCmd).ToArray();
                }
                conn.Close();
            }
            if (result.Length == 1)
                return result[0];
            else
                return null;
        }


        static void CollectChains(SentenceConstruct sen, Direction dir, int maxWords, SqliteCommand cmd)
        {
            const string searchSql = "SELECT followup FROM {0} WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
            cmd.CommandText = FormatSql(searchSql, dir);
            cmd.Prepare();

            while (sen.WordCount < maxWords)
            {
                string chain = GetLatestChain(sen, dir);
                cmd.Parameters.AddWithValue("@Chain", chain);
                
                var followUp = cmd.ExecuteScalar() as string;
                // If the chain couldn't be found (followUp is null) or if the chain is an ending chain (followUp is
                // empty) stop collecting chains.
                if ( !string.IsNullOrEmpty(followUp) )
                    AddTo(sen, dir, followUp);
                else
                    return;
            }
        }

        static string GetLatestChain(SentenceConstruct sen, Direction dir)
        {
            switch (dir)
            {
            case Direction.Forward:
                return sen.LatestForwardChain;
            case Direction.Backward:
                return sen.LatestBackwardChain;
            default:
                throw new InvalidOperationException("Unexpected Direction.");
            }
        }

        static void AddTo(SentenceConstruct sen, Direction dir, string followUp)
        {
            switch (dir)
            {
            case Direction.Forward:
                sen.Append(followUp);
                return;
            case Direction.Backward:
                sen.Prepend(followUp);
                return;
            }
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
            const string seedSql = "SELECT * FROM Forward WHERE chain LIKE @SeedPat OR followup LIKE @Seed " +
                "ORDER BY RANDOM()";
            if (cmd.CommandText != seedSql)
            {
                cmd.CommandText = seedSql;
                cmd.Prepare();
            }
            var seedPattern = string.Concat(seed, " %");
            cmd.Parameters.AddWithValue("@SeedPat", seedPattern);
            cmd.Parameters.AddWithValue("@Seed", seed);
        }
    }
}