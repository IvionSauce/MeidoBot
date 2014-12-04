using System;
using Mono.Data.Sqlite;


namespace Chainey
{
    internal class BuilderVectors : IDisposable
    {
        public SqliteCommand StartingChains { get; private set; }
        public SqliteCommand BackwardSearch { get; private set; }
        public SqliteCommand ForwardSearch { get; private set; }

        public string Source { get; set; }


        public BuilderVectors(SqliteConnection conn, string source)
        {
            StartingChains = conn.CreateCommand();
            BackwardSearch = conn.CreateCommand();
            ForwardSearch = conn.CreateCommand();

            Source = source;

            PrepareSearches();
        }


        void PrepareSearches()
        {
            const string forward = "SELECT Chains.forward ";
            const string backward = "SELECT Chains.backward ";
            
            if (string.IsNullOrEmpty(Source))
            {
                const string noSource = "FROM Chains WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
                
                BackwardSearch.CommandText = backward + noSource;
                ForwardSearch.CommandText = forward + noSource;
            }
            else
            {
                const string withSource = "FROM Chains, Sources, SrcMap " +
                    "WHERE Sources.source=@Source " +
                    "AND Sources.id=SrcMap.sId " +
                    "AND SrcMap.cId=Chains.id " +
                    "AND Chains.chain=@Chain ORDER BY RANDOM() LIMIT 1";
                
                BackwardSearch.CommandText = backward + withSource;
                ForwardSearch.CommandText = forward + withSource;
                
                BackwardSearch.Parameters.AddWithValue("@Source", Source);
                ForwardSearch.Parameters.AddWithValue("@Source", Source);
            }
        }


        public void PrepareSeedSql(string seed)
        {
            string seedSql;
            // Get chains with seed.
            if (string.IsNullOrEmpty(Source))
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
            
            if (StartingChains.CommandText != seedSql)
            {
                StartingChains.CommandText = seedSql;
                if (!string.IsNullOrEmpty(Source))
                    StartingChains.Parameters.AddWithValue("@Source", Source);
            }
            
            var seedPattern = string.Concat(seed, "%");
            StartingChains.Parameters.AddWithValue("@SeedPat", seedPattern);
        }


        public void PrepareRandomSql()
        {
            string randomSql;
            // Completely random.
            // In time this will probably have to be replaced with something more efficient.
            if (string.IsNullOrEmpty(Source))
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
            
            if (StartingChains.CommandText != randomSql)
            {
                StartingChains.CommandText = randomSql;
                if (!string.IsNullOrEmpty(Source))
                    StartingChains.Parameters.AddWithValue("@Source", Source);
            }
        }


        public void Dispose()
        {
            BackwardSearch.Dispose();
            ForwardSearch.Dispose();
            StartingChains.Dispose();
        }
    }
}