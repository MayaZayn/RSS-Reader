using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Xml;
using System.ServiceModel.Syndication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAntiforgery();
builder.Services.AddHttpClient();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connection = new SqliteConnection(connectionString);

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseHttpsRedirection();


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

app.MapPost("/sign-in", async () => {
    // Validate user
    
    // Redirect to home
    
});

app.MapGet("/home", (HttpContext context, IAntiforgery antiforgery) => {
    var tokens = antiforgery.GetAndStoreTokens(context);
    
    string htmlContent = $"""
        <section class="your-feed">
            <div class="container">
                <br>
                <h1 class="text-light text-center">Your Feed</h1>
                <div hx-get="/feeds" hx-trigger="load" class="accordion" id="accordion">
                    <!-- Display what's in db here initially -->
                </div>
            </div>
        </section>
        <section class="add-remove">
            <div class="add-button">
                <button type="button" class="btn btn-light btn-outline-dark mt-2 ms-2" data-bs-toggle="modal" data-bs-target="#addModal">
                    Add Feed
                </button>

                <div class="modal fade" id="addModal" tabindex="-1" aria-labelledby="addModalLabel" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title" id="addModalLabel">Add Feed</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <form hx-post="/feed/add" hx-target=".your-feed .container .accordion" hx-swap="beforeend" method="POST" id="addForm" enctype="multipart/form-data">
                                    <input type="hidden" name="__FormFieldName__" value="__RequestToken__">
                                    <div class="mb-3">
                                        <label for="feedUrl" class="form-label">Enter RSS/ATOM Feed URL</label>
                                        <input name="url" type="text" class="form-control" id="feedUrl" required>
                                    </div>
                                    <button type="submit" class="btn btn-dark">Add</button>
                                </form>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="remove-button">
                <button type="button" class="btn btn-light btn-outline-dark mt-2 ms-2" data-bs-toggle="modal" data-bs-target="#removeModal">
                    Remove Feed
                </button>

                <div class="modal fade" id="removeModal" tabindex="-1" aria-labelledby="removeModalLabel" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title" id="removeModalLabel">Remove Feed</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <form action="/feed/remove" method="POST" id="removeForm" enctype="multipart/form-data">
                                    <div class="btn-group">
                                        <button class="btn btn-danger dropdown-toggle" type="button" id="defaultDropdown" data-bs-toggle="dropdown" data-bs-auto-close="true" aria-expanded="false">
                                          Select a URL to delete
                                        </button>
                                        <ul hx-get="/feed/urls" hx-trigger="load" class="dropdown-menu" aria-labelledby="defaultDropdown">
                                            <!-- Display Selections to Remove Here -->
                                        </ul>
                                    </div>
                                </form>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    """;
    htmlContent = htmlContent.Replace("__RequestToken__", tokens.RequestToken);
    htmlContent = htmlContent.Replace("__FormFieldName__", tokens.FormFieldName);

    return Results.Content(htmlContent, "text/html");
});

app.MapGet("/feeds", async () =>
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
    
    return Results.Content(html, "text/html");
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
    
    return Results.Content(html, "text/html");
});

app.MapGet("/feed/urls", async () => {
    // Display Title as well
    connection.Open();
    var feeds = await connection.QueryAsync<Feed>("SELECT * FROM Feeds WHERE UserId = 1");
    string html = "";
    foreach (var feed in feeds) {
        html += $"""<li><button hx-delete="/feed/remove/{feed.UserId}/{feed.Id}" hx-target=".btn-group .dropdown-menu" hx-swap="innerHTML" class="dropdown-item">{feed.Url}</button></li>""";
    }
    return Results.Content(html, "text/html");
});

app.MapDelete("/feed/remove/{UserId}/{Id}", async (int UserId, string Id) => {
    connection.Open();
    var sqlCommand = "DELETE FROM Feeds WHERE Id = @Id AND UserId = @UserId";
    int rowsAffected = await connection.ExecuteAsync(sqlCommand, new { Id, UserId });

    var client = new HttpClient();
    var response = await client.GetAsync("http://localhost:5075/feed/urls");
    var content = await response.Content.ReadAsStringAsync();

    // var feeds = await connection.QueryAsync<Feed>("SELECT * FROM Feeds WHERE UserId = 1");
    // string html = "";
    // foreach (var feed in feeds) {
    //     html += $"""<li><button hx-delete="/feed/remove/{feed.UserId}/{feed.Id}" hx-target=".btn-group .dropdown-menu" hx-swap="innerHTML" class="dropdown-item">{feed.Url}</button></li>""";
    // }

    return Results.Content(content, "text/html");
});

app.Run();

public class Feed
{
    public required string Id { get; set; }
    public required int UserId { get; set; }
    // public string? Title { get; set; }
    public required string Url { get; set; }
}