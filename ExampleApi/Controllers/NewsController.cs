using ExampleApi.Data;
using ExampleApi.Model;
using ExampleApi.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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
        public List<News>? GetAll()
        {
            return _news.GetAllData();
        }

        [HttpPost]
        public Task<string> Post(News news)
        {
            var dateTime = DateTime.Now;

            var stringDate = dateTime.ToString("yyyy-MM-ddTHH\\:mm\\:ss");

            long unixTime = ((DateTimeOffset)dateTime).ToUnixTimeSeconds();

            news.Time = stringDate;
            news.TimeUnix = unixTime;

            return _news.InsertSingleAsync(news);
        }


        [HttpGet("title")]
        public async Task<List<News>?> DoSearchAsync(string title = "")
        {
            return await _news.ElasticSearchTitle(title);
        }
    }
}
