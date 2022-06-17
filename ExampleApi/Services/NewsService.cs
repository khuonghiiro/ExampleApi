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

        public List<News> GetNewsByPage(int page)
        {
            throw new NotImplementedException();
        }

        public async Task<string> InsertSingleAsync(News news)
        {
            await _elasticClient.IndexAsync<News>(news, x => x.Index("news"));

            string cacheKey = Convert.ToString(news.Id);

            string cachedDataString = JsonSerializer.Serialize(news);

            var dataToCache = Encoding.UTF8.GetBytes(cachedDataString);

            var options = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(DateTime.Now.AddMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(3));

            await _cache.SetAsync(cacheKey, dataToCache, options);

            return "Insert success!";
        }

        public List<News> SearchNews(string search)
        {
            throw new NotImplementedException();
        }

        public List<News>? GetAllData()
        {
            return NewsList;
        }
    }
}
