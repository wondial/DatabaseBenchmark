﻿using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using STS.General.Extensions;
using STS.General.Generators;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DatabaseBenchmark.Databases
{
    public class MongoDBDatabase : Database
    {
        private IMongoCollection<Row>[] collections;

        /// <summary>
        /// Specifies how many records are inserted with every batch.
        /// </summary>
        public int InsertsPerQuery { get; set; }

        public MongoDBDatabase()
        {
            Name = "MongoDB";
            CollectionName = "collection";
            Category = "NoSQL\\Document Store";
            Description = "MongoDB C# Driver 2.0.0beta";
            Website = "http://www.mongodb.org/";
            Color = Color.FromArgb(235, 215, 151);

            Requirements = new string[] 
            { 
                "MongoDB.Bson.dll", 
                "MongoDB.Driver.dll",
                "MongoDB.Driver.Core.dll",
                "MongoDB.Driver.Legacy.dll"
            };

            MongoUrlBuilder builder = new MongoUrlBuilder();
            builder.MaxConnectionIdleTime = TimeSpan.FromHours(1);
            builder.MaxConnectionLifeTime = TimeSpan.FromHours(1);
            builder.Server = new MongoServerAddress("localhost");
            builder.W = 1;

            ConnectionString = builder.ToString();
            InsertsPerQuery = 5000;
        }

        public override void Init(int flowCount, long flowRecordCount)
        {
            collections = new IMongoCollection<Row>[flowCount];

            for (int i = 0; i < flowCount; i++)
            {
                var client = new MongoClient(ConnectionString);
                IMongoDatabase database = client.GetDatabase("test");

                database.DropCollectionAsync(CollectionName).GetAwaiter().GetResult();

                var collection = database.GetCollection<Row>(CollectionName);
                collection.Indexes.CreateOneAsync(new IndexKeysDefinitionBuilder<Row>().Ascending("_id"));

                collections[i] = collection;
            }
        }

        public override void Write(int flowID, IEnumerable<KeyValuePair<long, Tick>> flow)
        {
            List<ReplaceOneModel<Row>> buffer = new List<ReplaceOneModel<Row>>(InsertsPerQuery * 2);

            foreach (var kv in flow)
            {
                Row row = new Row(kv.Key, kv.Value);

                ReplaceOneModel<Row> model = new ReplaceOneModel<Row>(new ObjectFilterDefinition<Row>(row), row);
                model.IsUpsert = true;

                buffer.Add(model);

                if (buffer.Count == InsertsPerQuery)
                {
                    var result = collections[flowID].BulkWriteAsync(buffer).Result;
                    buffer.Clear();
                }
            }

            if (buffer.Count != 0)
                collections[flowID].BulkWriteAsync(buffer);
        }

        public override IEnumerable<KeyValuePair<long, Tick>> Read()
        {
            IAsyncCursor<Row> enumerator = collections[0].Aggregate().Sort(new SortDefinitionBuilder<Row>().Ascending("_id")).ToCursorAsync().Result;

            while (enumerator.MoveNextAsync().Result)
            {
                foreach (var item in enumerator.Current)
                    yield return new KeyValuePair<long, Tick>(item._id, item.Record);
            }
        }

        public override void Finish()
        {
        }

        public override long Size
        {
            get
            {
                long sum = 0;

                // TODO: Finding another way to take the size.
                MongoClient clinet = new MongoClient(ConnectionString);
                var stat = clinet.GetServer().GetDatabase("test").GetCollection<Row>(CollectionName).GetStats();

                sum += stat.DataSize + stat.TotalIndexSize;

                return sum;
            }
        }

        private class Row
        {
            public long _id { get; set; }
            public Tick Record { get; set; }

            public Row()
            {
            }

            public Row(long key, Tick record)
            {
                _id = key;
                Record = record;
            }
        }
    }
}
