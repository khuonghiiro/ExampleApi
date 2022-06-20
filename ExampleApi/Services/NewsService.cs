using ExampleApi.Model;
using Microsoft.Extensions.Caching.Distributed;
using Nest;
using System.Text;
using System.Text.Json;

namespace ExampleApi.Services
{
    public class NewsService : INews
    {
        private List<News>? NewsList { get; set; }

        private readonly IDistributedCache _cache;

        private readonly IElasticClient _elasticClient;

        private const string KEY_CACHE = "news";

        public NewsService(IDistributedCache cache, IElasticClient elasticClient)
        {
            NewsList = Data.DataJson.LoadJson();
            _cache = cache;
            _elasticClient = elasticClient;
        }

        /// <summary>
        /// Insert single 1 news
        /// </summary>
        /// <param name="news"></param>
        /// <returns></returns>
        public async Task<string> InsertSingleAsync(News news)
        {
            string cacheKey = Convert.ToString(news.Id);

            // Insert data Redis
            await InsertDataRedis(news);

            // Insert data ElasticSearch
            await InsertDataES(news);

            if (NewsList != null)
            {
                // insert all data
                foreach (var item in NewsList)
                {
                    byte[]? cachedData = await _cache.GetAsync(Convert.ToString(item.Id));

                    var response = await _elasticClient.GetAsync<News>(new DocumentPath<News>(
                        new Id(news.Id)), x => x.Index("news"));

                    if (response.IsValid && cachedData != null)
                    {
                        continue;
                    }

                    // Insert data Redis
                    await InsertDataRedis(item);

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
        public async Task<List<News>?> SearchNewByPage(int page)
        {
            var response = await (_elasticClient.SearchAsync<News>(s => s
                                    .Index("news").Size(page)
                                ));

            return response.Hits.Select(s => s.Source).ToList();
        }

        public List<News>? GetAllData()
        {
            return NewsList;
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
        private async Task InsertDataRedis(News news)
        {
            string cacheKey = Convert.ToString(news.Id);

            byte[]? cachedData = await _cache.GetAsync(cacheKey);

            string cachedDataString = JsonSerializer.Serialize(news);

            var dataToCache = Encoding.UTF8.GetBytes(cachedDataString);

            var options = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(DateTime.Now.AddMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(3));

            // Insert data Redis
            await _cache.SetAsync(cacheKey, dataToCache, options);
        }

    }
}
