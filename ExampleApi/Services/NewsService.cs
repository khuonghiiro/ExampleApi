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
            //var response = await (_elasticClient.SearchAsync<News>(s => s
            //                        .Index("news").Size(page)
            //                    ));

            //return response.Hits.Select(s => s.Source).ToList();

            List<News>? newsPage = new();

            try
            {
                string cacheKey = "newsKey";

                byte[]? cachedData = await _cache.GetAsync(cacheKey);

                if (cachedData != null)
                {
                    List<News>? newsList = new();

                    // If the data is found in the cache, encode and deserialize cached data.
                    var cachedDataString = Encoding.UTF8.GetString(cachedData);
                    newsList = JsonSerializer.Deserialize<List<News>>(cachedDataString);

                    for (int i = 0; i < page; i++)
                    {
                        if (newsList != null)
                        {
                            newsPage.Add(newsList[i]);
                        }
                    }
                }
            }catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            
            return newsPage;
        }

        public async Task<List<News>?> GetAllData()
        {

            List<News>? newsList = new();

            string cacheKey = "newsKey";

            byte[]? cachedData = await _cache.GetAsync(cacheKey);

            if (cachedData != null)
            {
                // If the data is found in the cache, encode and deserialize cached data.
                var cachedDataString = Encoding.UTF8.GetString(cachedData);
                newsList = JsonSerializer.Deserialize<List<News>>(cachedDataString);
            }
            else
            {
                // If the data is not found in the cache, then fetch data from database
                newsList = NewsList;

                // Serializing the data
                string cachedDataString = JsonSerializer.Serialize(newsList);
                var dataToCache = Encoding.UTF8.GetBytes(cachedDataString);

                // Setting up the cache options
                // Sliding Expiration - A specific timespan within which the cache will expire if it is not used by anyone
                // Absolute Expiration - It refers to the actual expiration of the cache entry without considering the sliding expiration

                var options = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(DateTime.Now.AddMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(3));

                // Add the data into the cache
                await _cache.SetAsync(cacheKey, dataToCache, options);
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
        private async Task InsertDataRedis(News news)
        {
            string cacheKey = "newsKey";

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
