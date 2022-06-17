using ExampleApi.Services;
using Nest;

var builder = WebApplication.CreateBuilder(args);

// connect elastic search
var url = builder.Configuration["elasticsearch:url"];
//var defaultIndex = builder.Configuration["elasticsearch:index"];
var settings = new ConnectionSettings(new Uri(url));
builder.Services.AddSingleton<IElasticClient>(new ElasticClient(settings));

//redis cache
builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = builder.Configuration["RedisCacheUrl"]; });

// Register the corresponding Interface service with the Class object 
builder.Services.AddTransient<INews, NewsService>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
