using System;
using Mono.Data.Sqlite;


namespace Chainey
{
    internal class BuilderVectors : IDisposable
    {
        public SqliteCommand ChainCmd { get; private set; }
        public SqliteCommand BackwardCmd { get; private set; }
        public SqliteCommand ForwardCmd { get; private set; }

        readonly string source;


        public BuilderVectors(SqliteConnection conn, string source)
        {
            ChainCmd = conn.CreateCommand();
            BackwardCmd = conn.CreateCommand();
            ForwardCmd = conn.CreateCommand();

            this.source = source;

            CreateSearches();
        }


        void CreateSearches()
        {
            const string forward = "Chains.forward";
            const string backward = "Chains.backward";
            
            if (string.IsNullOrEmpty(source))
            {
                const string noSource = " FROM Chains WHERE chain=@Chain ORDER BY RANDOM() LIMIT 1";
                
                BackwardCmd.CommandText = "SELECT " + backward + noSource;
                ForwardCmd.CommandText = "SELECT " + forward + noSource;
            }
            else
            {
                const string withSource = " FROM Chains, Sources, SrcMap " +
                    "WHERE Sources.source=@Source " +
                    "AND Sources.id=SrcMap.sId " +
                    "AND SrcMap.cId=Chains.id " +
                    "AND Chains.chain=@Chain ORDER BY RANDOM() LIMIT 1";
                
                BackwardCmd.CommandText = "SELECT " + backward + withSource;
                ForwardCmd.CommandText = "SELECT " + forward + withSource;
                
                BackwardCmd.Parameters.AddWithValue("@Source", source);
                ForwardCmd.Parameters.AddWithValue("@Source", source);
            }
        }


        public void PrepareSeedSql(string seed)
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
            
            if (ChainCmd.CommandText != seedSql)
            {
                ChainCmd.CommandText = seedSql;
                if (!string.IsNullOrEmpty(source))
                    ChainCmd.Parameters.AddWithValue("@Source", source);
            }
            
            var seedPattern = string.Concat(seed, "%");
            ChainCmd.Parameters.AddWithValue("@SeedPat", seedPattern);
        }


        public void PrepareRandomSql()
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
            
            if (ChainCmd.CommandText != randomSql)
            {
                ChainCmd.CommandText = randomSql;
                if (!string.IsNullOrEmpty(source))
                    ChainCmd.Parameters.AddWithValue("@Source", source);
            }
        }


        public void Dispose()
        {
            BackwardCmd.Dispose();
            ForwardCmd.Dispose();
            ChainCmd.Dispose();
        }
    }
}