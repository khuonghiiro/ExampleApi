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

            // Insert data Redis
            await InsertSortedDataRedis(PREFIX, news, news.TimeUnix);

            // Insert data ElasticSearch
            await InsertDataES(news);

            if (NewsList != null)
            {
                // insert all data
                foreach (var item in NewsList)
                {
                    // Insert data Redis
                    await InsertSortedDataRedis(PREFIX, item, item.TimeUnix);

                    var response = await _elasticClient.GetAsync<News>(new DocumentPath<News>(
                        new Id(news.Id)), x => x.Index("news"));

                    if (response.IsValid)
                    {
                        continue;
                    }

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

            SortedSetEntry[] redisValueSorted = await _redisDBAsync.SortedSetRangeByRankWithScoresAsync(PREFIX, 0, page, Order.Ascending);

            foreach (SortedSetEntry item in redisValueSorted)
            {
                News? news = new();

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(item.Element.ToString());

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
                await InsertSortedDataRedis(PREFIX, NewsList[i], NewsList[i].TimeUnix);
            }

            SortedSetEntry[] redisValueSorted = await _redisDBAsync.SortedSetRangeByRankWithScoresAsync(PREFIX, 0, -1, Order.Ascending);

            foreach (var item in redisValueSorted)
            {
                News? news = new();

                news = Newtonsoft.Json.JsonConvert.DeserializeObject<News>(item.Element.ToString());

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

        private async Task InsertSortedDataRedis(string key, News news, double score)
        {
            RedisValue value = JsonSerializer.Serialize(news);

            await _redisDBAsync.SortedSetAddAsync(key, value, score);
        }
    }
}
