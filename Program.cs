using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;
using Dapper;
using System.ServiceModel.Syndication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Xml;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.LogoutPath = "/";
                    options.LoginPath = "/";
                });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connection = new SqliteConnection(connectionString);

connection.Execute(@"CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Email TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL
                    );");

connection.Execute(@"CREATE TABLE IF NOT EXISTS Feeds (
                        Id TEXT PRIMARY KEY,
                        UserId INTEGER NOT NULL,
                        Url TEXT NOT NULL,
                        FOREIGN KEY(UserId) REFERENCES Users(Id)
                    );");

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseHttpsRedirection();

app.MapGet("/", (HttpContext context) => {
    return Results.File("index.html", "text/html");
});

app.MapGet("/landing-page", (HttpContext context) =>
{
    string htmlContent = $"""
        <header>
            <nav class="navbar navbar-expand-lg navbar-light bg-light fixed-top">
                <div class="container-fluid">
                <div class="navbar-brand d-inline">Feed Reader</div>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarScroll" aria-controls="navbarScroll" aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="collapse navbar-collapse" id="navbarScroll">
                    <ul class="navbar-nav me-auto mb-2 mb-lg-0"></ul>
                    <button hx-get="/render/sign-in" hx-target="#mainContent" class="btn btn-outline-dark mx-2" type="submit">Sign In</button>
                    <button hx-get="/render/sign-up" hx-target="#mainContent" class="btn btn-outline-dark" type="submit">Sign Up</button>
                </div>
                </div>
            </nav>
        </header>
        <main>
            <div class="d-flex justify-content-center align-items-center vh-100">
                <div class="container mt-5 w-75">
                    <h1 class="text-center text-light fs-1 fw-bold">Welcome to Feed Reader</h1>
                    <p class="text-center text-light">Your seamless RSS feed reader for staying updated with all your favorite content in one place!</p>
                </div>
            </div>
        </main>
    """;

    return Results.Content(htmlContent, "text/html");
});

app.MapGet("/render/sign-in", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    
    string htmlContent = $"""
        <div class="d-flex justify-content-center align-items-center vh-100">
            <div class="container bg-light p-3 rounded w-50">
                <form hx-post="/sign-in" hx-target=".sign-in-form" hx-swap="beforeend" class="sign-in-form px-4 py-3" id="signInForm" enctype="multipart/form-data" >
                    <input type="hidden" name="{tokens.FormFieldName}" value="{tokens.RequestToken}">
                    <div class="mb-3">
                        <label for="formEmail" class="form-label">Email address</label>
                        <input type="email" name="email" class="form-control" id="formEmail" placeholder="email@example.com" required>
                    </div>
                    <div class="mb-3">
                        <label for="formPassword" class="form-label" >Password</label>
                        <input type="password" name="password" class="form-control" id="formPassword" placeholder="Password" required>
                    </div>
                    <button type="submit" class="btn btn-primary">Sign In</button>
                </form>
                <div class="dropdown-divider"></div>
                <button hx-get="/render/sign-up" hx-target="#mainContent" class="dropdown-item" type="button">Don't have an account? Sign up</button>
            </div>
        </div>
    """;

    return Results.Content(htmlContent, "text/html");
});

app.MapPost("/sign-in", async (
                            [FromForm]string email,
                            [FromForm]string password,
                            HttpContext context,
                            IAntiforgery antiforgery) => {

    await antiforgery.ValidateRequestAsync(context);
    
    using (connection = new SqliteConnection(connectionString)) 
    {
        var passwordHash = await connection.QuerySingleOrDefaultAsync<string>("SELECT PasswordHash FROM Users WHERE Email = @Email", new { Email = email });
        if (passwordHash != null && BCrypt.Net.BCrypt.Verify(password, passwordHash)) {
            var id = await connection.QuerySingleOrDefaultAsync<int>("SELECT Id FROM Users WHERE Email = @Email", new { Email = email });
            var claims = new List<Claim>
            {
                new(ClaimTypes.Email, email),
                new("UserId", id!.ToString()!)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties() 
            {
                IsPersistent = true
            };

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                                      new ClaimsPrincipal(claimsIdentity), authProperties);

            return Results.Content($"""
                <div class="alert alert-success mt-2" id="successAlert" role="alert">Signed In Successfully!</div>
                """, "text/html");
        }
    }

    return Results.Content($"""
                <div class="alert alert-danger mt-2" id="dangerAlert" role="alert">Invalid Credentials!</div>
                """, "text/html");
});

app.MapGet("/render/sign-up", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    
    string htmlContent = $"""
        <div class="d-flex justify-content-center align-items-center vh-100">
            <div class="container bg-light p-3 rounded w-50">
                <form class="px-4 py-3 sign-up-form" id="signUpForm" enctype="multipart/form-data">
                    <input type="hidden" name="{tokens.FormFieldName}" value="{tokens.RequestToken}">
                    <div class="mb-3">
                        <label for="formEmail" class="form-label">Email address</label>
                        <input type="email" name="email" class="form-control" id="formEmail" placeholder="email@example.com" required>
                    </div>
                    <div class="mb-3">
                        <label for="formPassword" class="form-label">Password</label>
                        <input type="password" name="password" class="form-control" id="formPassword" placeholder="Password" required>
                    </div>
                    <button hx-post="/sign-up" hx-target=".sign-up-form" hx-swap="beforeend" type="submit" class="btn btn-primary">Sign Up</button>
                </form>
                <div class="dropdown-divider"></div>
                <button hx-get="/render/sign-in" hx-target="#mainContent" class="dropdown-item" type="button">Already have an account? Sign In</button>
            </div>
        </div>
    """;

    return Results.Content(htmlContent, "text/html");
});

app.MapPost("/sign-up", async (
                            [FromForm]string email,
                            [FromForm]string password,
                            HttpContext context,
                            IAntiforgery antiforgery) => {

    await antiforgery.ValidateRequestAsync(context);
    
    using (connection = new SqliteConnection(connectionString))
    {
        User? userExists = await connection.QuerySingleOrDefaultAsync<User>("SELECT * FROM Users WHERE Email = @Email", new { Email = email });
        
        // user doesn't exist
        if (userExists == null) {
            User user = new() { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) };
            var sqlCommand = "INSERT INTO Users (Email, PasswordHash) VALUES (@Email, @PasswordHash)";
            int rowsAffected = await connection.ExecuteAsync(sqlCommand, user);
            return Results.Content($"""
                <div class="alert alert-success mt-2" id="successAlert" role="alert">Registration was successful!</div>
                """, "text/html");
        }
        return Results.Content($"""
                <div class="alert alert-danger mt-2" id="dangerAlert" role="alert">User already exists!</div>
                """, "text/html");
    }
});

app.MapPost("/sign-out", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/home", (HttpContext context, IAntiforgery antiforgery) => {
    var tokens = antiforgery.GetAndStoreTokens(context);
    
    string htmlContent = $"""
        <header>
            <nav class="navbar navbar-expand-lg navbar-light bg-light fixed-top">
                <div class="container-fluid">
                <div class="navbar-brand d-inline">Feed Reader</div>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarScroll" aria-controls="navbarScroll" aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="collapse navbar-collapse" id="navbarScroll">
                    <ul class="navbar-nav me-auto mb-2 mb-lg-0"></ul>
                    <button hx-post="/sign-out" hx-target="#mainContent" class="btn btn-outline-dark mx-2" type="submit">Sign Out</button>
                </div>
                </div>
            </nav>
        </header>
        <main>
        <section class="add-remove bg-light pb-2 pe-2 mt-100 rounded w-50 m-auto d-flex justify-content-between">
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
                                <form hx-post="/feed/add" hx-target="#addFormInput" hx-swap="beforeend" method="POST" id="addForm" enctype="multipart/form-data">
                                    <input type="hidden" name="{tokens.FormFieldName}" value="{tokens.RequestToken}">
                                    <div class="mb-3" id="addFormInput">
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
                                    <input type="hidden" name="{tokens.FormFieldName}" value="{tokens.RequestToken}">
                                    <div id="removeFormInput">
                                        <button class="btn btn-danger dropdown-toggle" type="button" id="defaultDropdown" data-bs-toggle="dropdown" data-bs-auto-close="true" aria-expanded="false">
                                          Select a URL to delete
                                        </button>
                                        <ul hx-get="/feed/urls" hx-trigger="every 1s" class="dropdown-menu" aria-labelledby="defaultDropdown">
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
        <section class="your-feed">
            <div class="container border border-1 rounded mt-3 py-3">
                <br>
                <h1 class="text-light text-center mb-5">Your Feed</h1>
                <div class="feed-container container rounded" id="feedContainer">
                    <div class="row">
                        <div class="col-md-3">
                            <div hx-get="/feeds" hx-trigger="every 2s" class="feed-container-1 container rounded border pt-3">
                                <!-- Display urls in db here initially -->
                            </div>
                        </div>
                        <div class="col-md-9">
                            <div class="feed-container-2 container rounded border pt-3 pb-3" id="feedContainer2">
                                <h2 class="text-muted text-center">Select a feed to view</h2>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
        </main>
    """;

    return Results.Content(htmlContent, "text/html");
}).RequireAuthorization();

app.MapGet("/feeds", async (ClaimsPrincipal claims) =>
{
    connection.Open();

    var email = claims.FindFirst(ClaimTypes.Email);
    if (email == null)
    {
        return Results.Redirect("/");
    }

    var feedUrls = await connection.QueryAsync<Feed>(
        "SELECT Feeds.Id, Feeds.UserId, Feeds.Url FROM Feeds INNER JOIN Users ON Feeds.UserId = Users.Id WHERE Users.Email = @Email",
        new { Email = email.Value }
    );

    connection.Close();

    string html = "";
    foreach (var feed in feedUrls) 
    {
        html += $"""
                <button class="btn btn-light feed-button" type="button" hx-get="/render/feed/{feed.Id}" hx-target=".feed-container-2"">
                    {feed.Url}
                </button>
                <br><br>
            """;
    }
    
    if (html == "" )
        html = "<h2 class=\"text-muted text-center pb-3\">No Feeds</h2>";

    return Results.Content(html, "text/html");
}).RequireAuthorization();

app.MapGet("/render/feed/{feedId}", async (string feedId) => {
    connection.Open();

    var feedUrl = await connection.QuerySingleOrDefaultAsync<string>("SELECT Url FROM Feeds WHERE Id = @feedId", new {feedId});
    connection.Close();

    Regex rtlRegex = new(@"[\u0590-\u08FF\u200F\u202A-\u202E\uFB1D-\uFB4F\uFE70-\uFEFF]");
    using var reader = XmlReader.Create(feedUrl!);
    var Feed = SyndicationFeed.Load(reader);
    string html = "";
    foreach (var item in Feed.Items)
    {
        string direction = "";
        if (rtlRegex.IsMatch(item.Summary.Text))
        {
            direction = "rtl"; // Text contains RTL characters
        }
        else
        {
            direction = "ltr"; // Text does not contain RTL characters
        }

        html += $"""  
                <div class="card">
                    <div class="card-header text-muted">
                        {item.PublishDate}
                    </div>
                    <div class="card-body">
                        <p class="card-text" dir="{direction}">{item.Summary.Text}</p>
                        <a href="{item.Links.FirstOrDefault()?.Uri}" class="btn btn-dark">Continue Reading</a>
                    </div>
                </div>
                <br>
                """;
    }

    return Results.Content(html, "text/html");
});

app.MapPost("/feed/add", async (
                        [FromForm] string url,
                        ClaimsPrincipal claims,
                        HttpContext context,
                        IAntiforgery antiforgery
                        ) =>
{
    await antiforgery.ValidateRequestAsync(context);

    try
    {
        using XmlReader reader = XmlReader.Create(url);
        SyndicationFeed sfeed = SyndicationFeed.Load(reader);

        var userId = claims.FindFirst("UserId");
        if (userId == null)
        {
            return Results.Redirect("/");
        }

        Feed feed = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = Convert.ToInt32(userId.Value),
            Url = url
        };

        connection.Open();

        var tmpFeed = await connection.QueryAsync<Feed>(
            "SELECT f.Id, f.UserId, f.Url FROM Feeds as f INNER JOIN Users as u ON f.UserId = u.Id WHERE u.Id = @Uid AND f.Url = @Url", 
            new { Uid = feed.UserId, Url = url });

        if (!tmpFeed.Any())
        {
            var sqlCommand = "INSERT INTO Feeds (Id, UserId, Url) VALUES (@Id, @UserId, @Url)";
            int rowsAffected = await connection.ExecuteAsync(sqlCommand, feed);

            connection.Close();

            return Results.Content($"""
                <div class="alert alert-success mt-2" id="successAlert" role="alert">Feed Added Successfully!</div>
                """, "text/html");
        }
        else
        {
            return Results.Content($"""
                <div class="alert alert-warning mt-2" id="warningAlert" role="alert">Feed already exists!</div>
                """, "text/html");
        }
    }
    catch (Exception)
    {
        return Results.Content($"""
        <div class="alert danger-warning mt-2" id="dangerAlert" role="alert">Please provide a valid RSS feed!</div>
        """, "text/html");
    }    
}).RequireAuthorization();

app.MapGet("/feed/urls", async (ClaimsPrincipal claims) => {
    connection.Open();
    var uid = claims.FindFirst("UserId");
    if (uid == null)
    {
        return Results.Redirect("/");
    }
    var feeds = await connection.QueryAsync<Feed>("SELECT f.Id, f.UserId, f.Url FROM Feeds as f INNER JOIN Users as u ON f.UserId = u.Id WHERE u.Id = @Uid", new { Uid =  Convert.ToInt32(uid.Value) });
    connection.Close();

    string html = "";
    foreach (var feed in feeds) {
        html += $"""<li><button hx-delete="/feed/remove/{feed.Id}" hx-target="#removeFormInput" hx-swap="beforeend" class="dropdown-item">{feed.Url}</button></li>""";
    }
    return Results.Content(html, "text/html");
});

app.MapDelete("/feed/remove/{Id}", async (ClaimsPrincipal claims, string Id) => {
    var uid = claims.FindFirst("UserId");
    if (uid == null)
    {
        return Results.Redirect("/");
    }

    connection.Open();
    var sqlCommand = "DELETE FROM Feeds WHERE Id = @Id AND UserId = @Uid";
    int rowsAffected = await connection.ExecuteAsync(sqlCommand, new { Id, Uid =  Convert.ToInt32(uid.Value) });
    connection.Close();

    return Results.Content($"""
    <div class="alert alert-success mt-2" id="successAlert" role="alert">Feed Removed Successfully!</div>
    """, "text/html");
}).RequireAuthorization();

app.MapGet("/public-feed", async (string email) =>
{
    connection.Open();
    var feedUrls = await connection.QueryAsync<Feed>("SELECT * FROM Feeds WHERE UserId = (SELECT Id FROM Users WHERE Email = @Email)", new { Email = email });
    connection.Close();
    if (feedUrls == null || !feedUrls.Any())
    {
        return Results.NotFound("No feeds found for the specified user.");
    }

    string html = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Feed Reader</title>
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.0.2/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-EVSTQN3/azprG1Anm3QDgpJLIm9Nao0Yz1ztcQTwFspd3yD65VohhpuuCOmLASjC" crossorigin="anonymous">
            <link href="/index.css" rel="stylesheet">
        </head>
        <body class="bg-dark">
            <div id="mainContent">
            <header>
                <nav class="navbar navbar-expand-lg navbar-light bg-light mb-2 sticky-top">
                    <div class="container-fluid">
                    <div class="navbar-brand d-inline">Feed Reader</div>
                    </div>
                </nav>
            </header>
            <main>
                <section class="your-feed">
                    <div class="container border border-1 rounded mt-3 py-3">
                        <br>
                        <h1 class="text-light text-center mb-5">Feed</h1>
                        <div class="feed-container container rounded" id="feedContainer">
                            <div class="row">
                                <div class="col-md-3">
                                    <div class="feed-container-1 container rounded border pt-3">
                                        <!-- Display urls in db here initially -->
    """;

    Regex rtlRegex = new(@"[\u0590-\u08FF\u200F\u202A-\u202E\uFB1D-\uFB4F\uFE70-\uFEFF]");

    var feedItems = new List<SyndicationItem>();
    foreach (var feed in feedUrls) {
        using var reader = XmlReader.Create(feed.Url);
        var Feed = SyndicationFeed.Load(reader);

        html += $"""
                <button class="btn btn-light feed-button" type="button" hx-get="/render/feed/{feed.Id}" hx-target=".feed-container-2"">
                    {feed.Url}
                </button>
                <br><br>
            """;
    }

    html += $"""
                                    </div>
                                </div>
                                <div class="col-md-9">
                                    <div class="feed-container-2 container rounded border pt-3 pb-3" id="feedContainer2">
                                        <h2 class="text-muted text-center">Select a feed to view</h2>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </section>
            </div>
            </main>
            <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.0.2/dist/js/bootstrap.bundle.min.js" integrity="sha384-MrcW6ZMFYlzcLA8Nl+NtUVF0sA7MsXsP1UyJoMp4YLEuNSfAP+JcXn/tWtIaxVXM" crossorigin="anonymous"></script>
            <script src="https://unpkg.com/htmx.org@1.9.12" integrity="sha384-ujb1lZYygJmzgSwoxRggbCHcjc0rB2XoQrxeTUQyRjrOnlCoYta87iKBWq3EsdM2" crossorigin="anonymous"></script>
        </body>
        </html>
    """;

    return Results.Content(html, "text/html");
});

app.Run();

public class Feed
{
    public required string Id { get; set; }
    public required int UserId { get; set; }
    public required string Url { get; set; }
}

public class User
{
    public string? Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
}
