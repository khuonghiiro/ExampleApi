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

        private const string PREFIX = "news";

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
            int number = Convert.ToInt32(NewsList?.Count) + 1;

            string Key = PREFIX + number;

            // Insert data Redis
            await InsertDataRedis(Key, news);

            // Insert data ElasticSearch
            await InsertDataES(news);

            if (NewsList != null)
            {
                int i = 0;
                // insert all data
                foreach (var item in NewsList)
                {
                    var keyRedis = new RedisKey();

                    keyRedis = PREFIX + i;

                    var isKeyRedis = await _redisDBAsync.KeyExistsAsync(keyRedis);

                    var response = await _elasticClient.GetAsync<News>(new DocumentPath<News>(
                        new Id(news.Id)), x => x.Index("news"));

                    if (response.IsValid && isKeyRedis)
                    {
                        continue;
                    }

                    // Insert data Redis
                    await InsertDataRedis(keyRedis.ToString(), item);

                    // Insert data ElasticSearch
                    await InsertDataES(item);

                    i++;
                }
            }

            return "Insert ElasticSearch and Redis success!";
        }

        /// <summary>
        /// Search with size page
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public List<News>? SearchNewByPage(int page)
        {
            List<News>? newsPage = new();

            var keyList = new RedisKey[page];
            //Generate keys array
            for (int i = 0; i < page; i++)
            {
                var key = new RedisKey();
                key = PREFIX + i;
                keyList.SetValue(key, i);
            }

            Task<RedisValue[]> values = _redisDBAsync.SetCombineAsync(SetOperation.Union, keyList);

            foreach (var value in values.Result)
            {
                News? news = new();

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(value.ToString());

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

            for (int i = 0; i < NewsList?.Count; i++)
            {
                //RedisValue value = JsonSerializer.Serialize(NewsList[i]);
                //await _redisDBAsync.SetAddAsync(PREFIX + i, value);
                await InsertDataRedis(PREFIX + i, NewsList[i]);
            }

            var keyList = new RedisKey[number];
            //Generate keys array
            for (int i = 0; i < number; i++)
            {
                var key = new RedisKey();
                key = PREFIX + i;
                keyList.SetValue(key, i);
            }

            Task<RedisValue[]> values = _redisDBAsync.SetCombineAsync(SetOperation.Union, keyList);

            for (int i = 0; i < number; i++)
            {
                News? news = new();

                string? value = values.Result[i].ToString();

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(value);

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
        private async Task InsertDataRedis(string key, News news)
        {
            RedisValue value = JsonSerializer.Serialize(news);

            await _redisDBAsync.SetAddAsync(key, value);
        }
    }
}
