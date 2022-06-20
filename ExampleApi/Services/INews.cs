using ExampleApi.Model;

namespace ExampleApi.Services
{
    public interface INews
    {
        public Task<List<News>?> GetAllData();

        public Task<string> InsertSingleAsync(News news);

        public Task<List<News>?> SearchNewByPage(int page);

        public Task<List<News>?> ElasticSearchTitle(string title);
    }
}
