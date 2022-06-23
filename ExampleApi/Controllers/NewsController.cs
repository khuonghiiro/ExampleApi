using ExampleApi.Model;
using ExampleApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsController : ControllerBase
    {
        private readonly INews _news;

        public NewsController(INews news)
        {
            _news = news;
        }

        [HttpGet]
        public Task<List<News>?> GetAll()
        {
            return _news.GetAllData();
        }

        [HttpGet("GetAll/{id}")]
        public News? GetNewsById(string id)
        {
            return _news.GetElementById(id).Result;
        }

        [HttpGet("{page}")]
        public List<News>? GetNewsByPage(int page)
        {
            return _news.SearchNewByPageAsync(page).Result;
        }

        [HttpPost]
        public Task<string> Post(News news)
        {
            return _news.InsertSingleAsync(news);
        }

        [HttpGet("title")]
        public async Task<List<News>?> DoSearchAsync(string title = "")
        {
            return await _news.ElasticSearchTitle(title);
        }
    }
}
