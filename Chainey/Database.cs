using System;
using System.Linq;
using System.Data;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using IvionSoft;
using System.Diagnostics;

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
                    throw new ArgumentOutOfRangeException("value", "MaxWords cannot be 0 or negative.");
            }
        }

        readonly ThreadLocalSqlite localSqlite;

        private enum Direction
        {
            Forward,
            Backward
        }


        public SqliteBrain(string file, int order)
        {
            file.ThrowIfNullOrWhiteSpace("file");
            if (order < 1)
                throw new ArgumentOutOfRangeException("order", "Cannot be 0 or negative.");

            localSqlite = new ThreadLocalSqlite(file);
            Order = order;
            MaxWords = 100;

            using (var connection = new SqliteConnection("URI=file:" + file))
            {
                connection.Open();

                using (var cmd = new SqliteCommand(connection))
                {
                    // ----- Populate the database with tables, if necessary -----
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS WordCount(" +
                        "word TEXT PRIMARY KEY COLLATE NOCASE," +
                        "count INTEGER NOT NULL DEFAULT 1) WITHOUT ROWID";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS Chains(" +
                        "id INTEGER PRIMARY KEY," +
                        "backward TEXT NOT NULL DEFAULT ''," +
                        "chain TEXT NOT NULL," +
                        "forward TEXT NOT NULL DEFAULT ''," +
                        "UNIQUE(chain, backward, forward))";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS Sources(" +
                        "id INTEGER PRIMARY KEY," +
                        "source TEXT NOT NULL," +
                        "UNIQUE(source))";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS SrcMap(" +
                        "sId INTEGER," +
                        "cId INTEGER," +
                        "FOREIGN KEY(sId) REFERENCES Sources(id)," +
                        "FOREIGN KEY(cId) REFERENCES Chains(id) ON DELETE CASCADE," +
                        "UNIQUE(sId, cId)," +
                        "UNIQUE(cId, sId))";
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
        /// <param name="source">Source of the sentence, will be ignored if null or empty.</param> 
        public void AddSentence(string[] sentenceWords, string source)
        {
            if (sentenceWords == null)
                throw new ArgumentNullException("sentenceWords");
            // Return early if there's nothing to do.
            else if (sentenceWords.Length < Order)
                return;
            
            string[][] chains = MarkovTools.TokenizeSentence(sentenceWords, Order);

            var sw = Stopwatch.StartNew();

            var conn = localSqlite.GetDb();
            using (SqliteTransaction tr = conn.BeginTransaction())
            using (SqliteCommand insertCmd = conn.CreateCommand(),
                   updateCmd = conn.CreateCommand())
            {
                insertCmd.Transaction = tr;
                updateCmd.Transaction = tr;

                if (!string.IsNullOrEmpty(source))
                {
                    // Make sure `source` exists in Sources.
                    updateCmd.CommandText = "INSERT OR IGNORE INTO Sources VALUES(null, @Source)";
                    updateCmd.Parameters.AddWithValue("@Source", source);
                    updateCmd.ExecuteNonQuery();
                }

                UpdateWordCount(sentenceWords, insertCmd);
                InsertChains(chains, source, insertCmd, updateCmd);

                try
                {
                    tr.Commit();
                }
                catch (SqliteException)
                {
                    Console.WriteLine("!! ERROR ADDING: " + string.Join(" ", sentenceWords));
                    Console.WriteLine("!! AddSentence time: " + sw.Elapsed);
                    throw;
                }
                finally
                {
                    sw.Stop();
                }
            }
        }


        // Insert chains and add/update relevant Source-mapping to SrcMap.
        static void InsertChains(string[][] chains, string source, SqliteCommand cmd, SqliteCommand updateCmd)
        {
            cmd.CommandText = "INSERT OR IGNORE INTO Chains VALUES(null, @Backward, @Chain, @Forward)";

            updateCmd.CommandText = "INSERT OR IGNORE INTO SrcMap VALUES(" +
                "(SELECT id FROM Sources WHERE source=@Source), " +
                "(SELECT id FROM Chains WHERE backward=@Backward AND chain=@Chain AND forward=@Forward))";
            updateCmd.Parameters.AddWithValue("@Source", source);

            var backward = string.Empty;
            foreach (string[] chain in chains)
            {
                var insertChain = string.Join(" ", chain, 0, chain.Length - 1);
                var forward = chain[chain.Length - 1] ?? string.Empty;

                cmd.Parameters.AddWithValue("@Backward", backward);
                cmd.Parameters.AddWithValue("@Chain", insertChain);
                cmd.Parameters.AddWithValue("@Forward", forward);
                cmd.ExecuteNonQuery();

                if (!string.IsNullOrEmpty(source))
                {
                    updateCmd.Parameters.AddWithValue("@Backward", backward);
                    updateCmd.Parameters.AddWithValue("@Chain", insertChain);
                    updateCmd.Parameters.AddWithValue("@Forward", forward);
                    updateCmd.ExecuteNonQuery();
                }

                backward = chain[0];
            }
        }


        // ***
        // -------------------------------
        // Methods for removing sentences.
        // -------------------------------
        // ***

        public void RemoveSentence(string[] sentenceWords)
        {
            if (sentenceWords == null)
                throw new ArgumentNullException("sentenceWords");
            // Return early if there's nothing to do.
            else if (sentenceWords.Length < Order)
                return;
            
            string[][] chains = MarkovTools.TokenizeSentence(sentenceWords, Order);

            var conn = localSqlite.GetDb();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                cmd.ExecuteNonQuery();

                using (var tr = conn.BeginTransaction())
                {
                    cmd.Transaction = tr;
                    DeleteChains(chains, cmd);
                    tr.Commit();
                }
            }
        }


        static void DeleteChains(string[][] chains, SqliteCommand cmd)
        {
            const string deleteSql = "DELETE FROM Chains WHERE chain=@Chain " +
                "AND backward=@Backward AND forward=@Forward";
            cmd.CommandText = deleteSql;

            var backward = string.Empty;
            foreach (string[] chain in chains)
            {
                var deleteChain = string.Join(" ", chain, 0, chain.Length - 1);
                var forward = chain[chain.Length - 1] ?? string.Empty;
                
                cmd.Parameters.AddWithValue("@Chain", deleteChain);
                cmd.Parameters.AddWithValue("@Backward", backward);
                cmd.Parameters.AddWithValue("@Forward", forward);
                cmd.ExecuteNonQuery();

                backward = chain[0];
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

            var conn = localSqlite.GetDb();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = countSql;

                foreach (string word in words)
                    yield return WordCount(word, cmd);
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
            // `word` will never be empty, since calling methods guard against that.
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


        public IEnumerable<string> BuildSentences(IEnumerable<string> seeds, string source)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            var conn = localSqlite.GetDb();
            using (SqliteCommand chainCmd = conn.CreateCommand(),
                   collectCmd = conn.CreateCommand())
            {
                foreach (string seed in seeds)
                {
                    if (string.IsNullOrEmpty(seed))
                        continue;

                    CreateSeedSql(chainCmd, seed, source);

                    foreach (string sentence in Builder(source, chainCmd, collectCmd))
                        yield return sentence;
                }
            }
        }

        static void CreateSeedSql(SqliteCommand cmd, string seed, string source)
        {
            string seedSql;
            // Get chains with seed.
            if (string.IsNullOrEmpty(source))
            {
                seedSql = "SELECT backward, chain, forward FROM Chains " +
                    "WHERE chain LIKE @SeedPat OR forward LIKE @SeedPat ORDER BY RANDOM()";
            }
            // Get chains with seed, but constrained by source.
            else
            {
                seedSql = "SELECT Chains.backward, Chains.chain, Chains.forward FROM Chains, Sources, SrcMap " +
                    "WHERE Sources.source=@Source " +
                    "AND Sources.id=SrcMap.sId " +
                    "AND SrcMap.cId=Chains.id " +
                    "AND (Chains.chain LIKE @SeedPat OR Chains.forward LIKE @SeedPat) ORDER BY RANDOM()";
            }

            if (cmd.CommandText != seedSql)
            {
                cmd.CommandText = seedSql;
                if (!string.IsNullOrEmpty(source))
                    cmd.Parameters.AddWithValue("@Source", source);
            }

            var seedPattern = string.Concat(seed, "%");
            cmd.Parameters.AddWithValue("@SeedPat", seedPattern);
        }


        public IEnumerable<string> BuildRandomSentences(string source)
        {
            var conn = localSqlite.GetDb();
            using (SqliteCommand chainCmd = conn.CreateCommand(),
                   collectCmd = conn.CreateCommand())
            {
                CreateRandomSql(chainCmd, source);

                foreach (string sentence in Builder(source, chainCmd, collectCmd))
                    yield return sentence;
            }
        }

        void CreateRandomSql(SqliteCommand cmd, string source)
        {
            string randomSql;
            // Completely random.
            // In time this will probably have to be replaced with something more efficient.
            if (string.IsNullOrEmpty(source))
                randomSql = "SELECT backward, chain, forward FROM Chains ORDER BY RANDOM()";
            // Random, but constrained to chains originating from `source`.
            else
            {
                randomSql = "SELECT Chains.backward, Chains.chain, Chains.forward " +
                    "FROM Chains, Sources, SrcMap " +
                    "WHERE Sources.source=@Source " +
                    "AND Sources.id=SrcMap.sId " +
                    "AND SrcMap.cId=Chains.id ORDER BY RANDOM()";
            }

            if (cmd.CommandText != randomSql)
            {
                cmd.CommandText = randomSql;
                if (!string.IsNullOrEmpty(source))
                    cmd.Parameters.AddWithValue("@Source", source);
            }
        }


        IEnumerable<string> Builder(string source, SqliteCommand chainCmd, SqliteCommand collectCmd)
        {
            using (var reader = chainCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string backward = reader.GetString(0);
                    string chain = reader.GetString(1);
                    string forward = reader.GetString(2);

                    var words = new SentenceConstruct(chain);

                    // Copy out the volatile field, so we have a consistent MaxWords while building the sentence.
                    int maxWords = MaxWords;

                    if (backward != string.Empty)
                    {
                        // Backward search.
                        words.Prepend(backward);
                        CollectChains(words, source, Direction.Backward, maxWords, collectCmd);
                    }

                    if (forward != string.Empty)
                    {
                        // Forward search.
                        words.Append(forward);
                        CollectChains(words, source, Direction.Forward, maxWords, collectCmd);
                    }

                    yield return words.Sentence;
                }
            }
        }


        // ***
        // --------------------------------------------------------------------------
        // Methods for collecting chains and pushing them unto the SentenceConstruct.
        // --------------------------------------------------------------------------
        // ***

        static void CollectChains(SentenceConstruct sen, string source, Direction dir, int maxWords, SqliteCommand cmd)
        {
            PrepareSearch(cmd, dir, source);

            while (sen.WordCount < maxWords)
            {
                string chain = GetLatestChain(sen, dir);
                // Seems like Add is slightly faster than AddWithValue, although the difference is so small it only
                // makes sense to use it in this loop, which can get called 10's of thousands of times.
                cmd.Parameters.Add("@Chain", DbType.String).Value = chain;
                //cmd.Parameters.AddWithValue("@Chain", chain);

                var followUp = cmd.ExecuteScalar() as string;
                // If the chain couldn't be found (followUp is null) or if the chain is an ending chain (followUp is
                // empty) stop collecting chains.
                if ( !string.IsNullOrEmpty(followUp) )
                    AddTo(sen, dir, followUp);
                else
                    return;
            }
        }

        static void PrepareSearch(SqliteCommand cmd, Direction dir, string source)
        {
            string column;
            switch(dir)
            {
            case Direction.Forward:
                column = "Chains.forward";
                break;
            case Direction.Backward:
                column = "Chains.backward";
                break;
            default:
                throw new InvalidOperationException("Unexpected Direction.");
            }

            string searchSql;
            if (string.IsNullOrEmpty(source))
                searchSql = "SELECT " + column + " FROM Chains WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
            else
            {
                searchSql = "SELECT " + column + " FROM Chains, Sources, SrcMap " +
                    "WHERE Sources.source=@Source " +
                    "AND Sources.id=SrcMap.sId " +
                    "AND SrcMap.cId=Chains.id " +
                    "AND Chains.chain=@Chain ORDER BY RANDOM() LIMIT 1";
            }

            if (cmd.CommandText != searchSql)
            {
                cmd.CommandText = searchSql;
                cmd.Parameters.AddWithValue("@Source", source);
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

    }
}