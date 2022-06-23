using ExampleApi.Model;
using Microsoft.Extensions.Caching.Distributed;
using Nest;
using StackExchange.Redis;
using System.Text.Json;

namespace ExampleApi.Services
{
    public class NewsService : INews
    {
        private List<News>? NewsList { get; set; }

        private readonly IDistributedCache _cache;
        private readonly IConnectionMultiplexer _redisCache;
        private readonly IDatabaseAsync _redisDBAsync;
        private readonly IElasticClient _elasticClient;

        private const string SORTED_KEY = "newsSorted";
        private const string HASH_KEY = "newsHash";

        public NewsService(IDistributedCache cache, IElasticClient elasticClient, IConnectionMultiplexer redisCache)
        {
            NewsList = Data.DataJson.LoadJson();
            _cache = cache;
            _elasticClient = elasticClient;
            _redisCache = redisCache;
            _redisDBAsync = _redisCache.GetDatabase();
        }

        /// <summary>
        /// Insert single 1 news
        /// </summary>
        /// <param name="news"></param>
        /// <returns></returns>
        public async Task<string> InsertSingleAsync(News news)
        {
            int number = Convert.ToInt32(NewsList?.Count);

            string value = JsonSerializer.Serialize(news);

            // Insert data Redis
            await _redisDBAsync.SortedSetAddAsync(SORTED_KEY, news.Id.ToString(), news.TimeUnix);

            //add data in datatype hash
            await _redisDBAsync.HashSetAsync(HASH_KEY, new HashEntry[] { new HashEntry(news.Id.ToString(), value) });

            // Insert data ElasticSearch
            await InsertDataES(news);

            if (NewsList != null)
            {
                // insert all data
                foreach (var item in NewsList)
                {
                    string valueItem = JsonSerializer.Serialize(item);

                    // Insert data in datattype of Redis
                    await _redisDBAsync.SortedSetAddAsync(SORTED_KEY, item.Id.ToString(), item.TimeUnix);

                    var response = await _elasticClient.GetAsync<News>(new DocumentPath<News>(
                        new Id(news.Id)), x => x.Index("news"));

                    if (response.IsValid &&
                        (await _redisDBAsync.HashExistsAsync(HASH_KEY, item.Id.ToString())))
                    {
                        continue;
                    }

                    // Insert data in datatype Hash of Redis
                    await _redisDBAsync.HashSetAsync(HASH_KEY, new HashEntry[] { new HashEntry(item.Id.ToString(), valueItem) });

                    // Insert data ElasticSearch
                    await InsertDataES(item);
                }
            }

            return "Insert ElasticSearch and Redis success!";
        }

        /// <summary>
        /// Search with size page
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<List<News>?> SearchNewByPageAsync(int page)
        {
            List<News>? newsPage = new();

            SortedSetEntry[] redisValueSorted = await _redisDBAsync.SortedSetRangeByRankWithScoresAsync(SORTED_KEY, 0, page-1, Order.Ascending);

            foreach (SortedSetEntry item in redisValueSorted)
            {
                News? news = new();

                string keyNewsHash = item.Element.ToString();

                if (!(await _redisDBAsync.HashExistsAsync(HASH_KEY, keyNewsHash)))
                {
                    continue;
                }

                var keyNewsValue = await _redisDBAsync.HashGetAsync(HASH_KEY, keyNewsHash);

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(keyNewsValue.ToString());

                if (news != null)
                {
                    newsPage.Add(news);
                }
            }

            return newsPage;
        }

        public async Task<List<News>?> GetAllData()
        {
            List<News>? newsList = new();

            int number = Convert.ToInt32(NewsList?.Count);

            HashEntry[] redisNewsHash = new HashEntry[number];

            for (int i = 0; i < NewsList?.Count; i++)
            {
                string id = NewsList[i].Id.ToString();
                string value = JsonSerializer.Serialize(NewsList[i]);

                // add data in datatype sortedset 
                await _redisDBAsync.SortedSetAddAsync(SORTED_KEY, id, NewsList[i].TimeUnix);

                redisNewsHash[i] = new HashEntry(id, value);
            }

            // add data in datatype hash 
            await _redisDBAsync.HashSetAsync(HASH_KEY, redisNewsHash);

            SortedSetEntry[] redisValueSorted = await _redisDBAsync.SortedSetRangeByRankWithScoresAsync(SORTED_KEY, 0, -1, Order.Ascending);

            foreach (var item in redisValueSorted)
            {
                News? news = new();

                string keyNewsHash = item.Element.ToString();

                if (!(await _redisDBAsync.HashExistsAsync(HASH_KEY, keyNewsHash)))
                {
                    continue;
                }

                var keyNewsValue = await _redisDBAsync.HashGetAsync(HASH_KEY, keyNewsHash);

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(keyNewsValue.ToString());

                if (news != null)
                {
                    newsList.Add(news);
                }
            }

            return newsList;
        }

        /// <summary> 
        /// Search with title in data.json
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public async Task<List<News>?> ElasticSearchTitle(string title)
        {
            var response = await (_elasticClient.SearchAsync<News>(s => s
                                    .Index("news")
                                    .Query(q => q
                                        .MultiMatch(m => m.Query(title).
                                        Fields(f => f.Field(v => v.Title)).
                                        Fuzziness(Fuzziness.Auto)
                                        )
                                    ).Size(3).From(0)
                                ));

            return response.Hits.Select(s => s.Source).ToList();
        }

        /// <summary>
        /// Insert data in ElasticSearch
        /// </summary>
        /// <param name="news">Object data model News</param>
        /// <returns></returns>
        private async Task InsertDataES(News news)
        {
            // Insert data ElasticSearch
            await _elasticClient.IndexAsync<News>(news, x => x.Index("news").Refresh(Elasticsearch.Net.Refresh.True));
        }

        /// <summary>
        /// Insert data in Redis
        /// </summary>
        /// <param name="news">Object data model News</param>
        /// <returns></returns>
        private async Task InsertSetDataRedis(string key, News news)
        {
            RedisValue value = JsonSerializer.Serialize(news);

            await _redisDBAsync.SetAddAsync(key, value);
        }

        public async Task<News?> GetElementById(string id)
        {
            News? news = new();

            if (await _redisDBAsync.HashExistsAsync(HASH_KEY, id))
            {
                var keyNewsValue = await _redisDBAsync.HashGetAsync(HASH_KEY, id);

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(keyNewsValue.ToString());
            }

            return news;
        }
    }
}
