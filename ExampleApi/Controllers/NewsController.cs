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

        [HttpGet("{page}")]
        public Task<List<News>?> GetNewsByPage(int page)
        {
            return _news.SearchNewByPage(page);
        }

        [HttpPost]
        public Task<string> Post(News news)
        {
            //var dateTime = DateTime.Now;

            //var stringDate = dateTime.ToString("yyyy-MM-ddTHH\\:mm\\:ss");

            //long unixTime = ((DateTimeOffset)dateTime).ToUnixTimeSeconds();

            //news.Time = stringDate;
            //news.TimeUnix = unixTime*1000;

            return _news.InsertSingleAsync(news);
        }


        [HttpGet("title")]
        public async Task<List<News>?> DoSearchAsync(string title = "")
        {
            return await _news.ElasticSearchTitle(title);
        }
    }
}
