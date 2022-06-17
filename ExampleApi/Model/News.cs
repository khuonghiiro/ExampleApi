using System;

namespace ExampleApi.Model
{
    public class News
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string? Desc { get; set; }
        public string? Body { get; set; }
        public string? Time { get; set; }
        public long TimeUnix { get; set; }
        public string? Link { get; set; }
        public string? Source { get; set; }
        public int Type { get; set; }
        public string? CatName { get; set; }
        public string? Tags { get; set; }
        public int? ClusterId { get; set; }
    }
}
