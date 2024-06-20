using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Xml;
using System.ServiceModel.Syndication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAntiforgery();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connection = new SqliteConnection(connectionString);

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();


connection.Execute(@"CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Email TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL
                    );");

// connection.Execute(@"DROP TABLE Feeds");

connection.Execute(@"CREATE TABLE IF NOT EXISTS Feeds (
                        Id TEXT PRIMARY KEY,
                        UserId INTEGER NOT NULL,
                        Url TEXT NOT NULL,
                        FOREIGN KEY(UserId) REFERENCES Users(Id)
                    );");

// TODO::home

// TODO::display stuff from db

// TODO::display stuff from db into selections

// TODO:: sign in

// TODO:: sign up

app.MapGet("/", async (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    
    string htmlContent = await File.ReadAllTextAsync("wwwroot/index.html");
    htmlContent = htmlContent.Replace("__RequestToken__", tokens.RequestToken);
    htmlContent = htmlContent.Replace("__FormFieldName__", tokens.FormFieldName);

    return Results.Content(htmlContent, "text/html");
});

app.MapGet("/api/feeds", async () =>
{
    connection.Open();

    var feedUrls = await connection.QueryAsync<Feed>("SELECT * FROM Feeds WHERE UserId = 1");

    var feedItems = new List<SyndicationItem>();
    string html = "";
    foreach (var feed in feedUrls) {
        using var reader = XmlReader.Create(feed.Url);
        var Feed = SyndicationFeed.Load(reader);

        html += $"""
            <div class="accordion-item">
                <h2 class="accordion-header" id="heading{feed.Id}">
                <button class="accordion-button" type="button" data-bs-toggle="collapse" data-bs-target="#collapse{feed.Id}" aria-expanded="true" aria-controls="collapse{feed.Id}">
                    Title
                </button>
                </h2>
                <div id="collapse{feed.Id}" class="accordion-collapse collapse" aria-labelledby="heading{feed.Id}" data-bs-parent="#accordion">
                <div class="accordion-body">
            """;

        foreach (var item in Feed.Items)
        {
            html += $"""  
                    <div class="card text-center">
                    <div class="card-header">
                        {feed.Url}
                    </div>
                    <div class="card-body">
                        <h5 class="card-title">Special title treatment</h5>
                        <p class="card-text">{item.Summary.Text}</p>
                        <a href="{item.Links.FirstOrDefault()?.Uri}" class="btn btn-dark">Continue Reading</a>
                    </div>
                    <div class="card-footer text-muted">
                        {item.LastUpdatedTime:MMM dd, yyyy hh:mm tt}
                    </div>
                    </div>
                    <br>
                    """;
        }

        html += "</div></div></div>";
    }
    
    return Results.Content(html);
});

app.MapPost("/feed/add", async (
                        [FromForm] string url, 
                        HttpContext context,
                        IAntiforgery antiforgery
                        ) =>
{
    await antiforgery.ValidateRequestAsync(context);

    Feed feed = new() { 
        Id = Guid.NewGuid().ToString(),
        UserId = 1,
        Url = url
    };

    connection.Open();

    var sqlCommand = "INSERT INTO Feeds (Id, UserId, Url) VALUES (@Id, @UserId, @Url)";
    int rowsAffected = await connection.ExecuteAsync(sqlCommand, feed);

    var feedItems = new List<SyndicationItem>();
    using var reader = XmlReader.Create(url);
    var Feed = SyndicationFeed.Load(reader);
    feedItems.AddRange(Feed.Items);

    string html = $"""
            <div class="accordion-item">
                <h2 class="accordion-header" id="heading{feed.Id}">
                <button class="accordion-button" type="button" data-bs-toggle="collapse" data-bs-target="#collapse{feed.Id}" aria-expanded="true" aria-controls="collapse{feed.Id}">
                    Title
                </button>
                </h2>
                <div id="collapse{feed.Id}" class="accordion-collapse collapse" aria-labelledby="heading{feed.Id}" data-bs-parent="#accordion">
                <div class="accordion-body">
            """;

    foreach (var item in feedItems)
    {
        html += $"""  
                <div class="card text-center">
                <div class="card-header">
                    {feed.Url}
                </div>
                <div class="card-body">
                    <h5 class="card-title">Special title treatment</h5>
                    <p class="card-text">{item.Summary.Text}</p>
                    <a href="{item.Links.FirstOrDefault()?.Uri}" class="btn btn-dark">Continue Reading</a>
                </div>
                <div class="card-footer text-muted">
                    2 days ago
                </div>
                </div>
                <br>
                """;
    }

    html += "</div></div></div>";
    
    return Results.Content(html);
});




app.Run();

public class Feed
{
        public required string Id { get; set; }
        public required int UserId { get; set; }
        // public string? Title { get; set; }
        public required string Url { get; set; }
}