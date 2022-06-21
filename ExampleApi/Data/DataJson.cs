using ExampleApi.Model;
using Newtonsoft.Json;

namespace ExampleApi.Data
{
    public static class DataJson
    {
        private static readonly string FilePath = Path.Join(Environment.CurrentDirectory, "Data/data.json");

        public static List<News>? LoadJson()
        {
            List<News>? list = new();

            using (StreamReader read = new(FilePath))
            {
                string json = read.ReadToEnd();
                list = JsonConvert.DeserializeObject<List<News>>(json);
            }

            return list;
        }
    }
}
