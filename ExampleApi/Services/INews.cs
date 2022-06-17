using ExampleApi.Model;

namespace ExampleApi.Services
{
    public interface INews
    {
        public List<News>? GetAllData();

        public Task<string> InsertSingleAsync(News news);

        public List<News>? GetNewsByPage(int page);

        public List<News>? SearchNews(string search);
    }
}
