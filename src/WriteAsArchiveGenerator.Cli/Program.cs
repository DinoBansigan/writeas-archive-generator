using Microsoft.Extensions.Configuration;
using WriteAs.NET;
using WriteAsArchiveGenerator.Core;

const string Usage = """
    Generates archive page HTML for a Write.as blog.

    Usage:
      WriteAsArchiveGenerator <alias> [--output <file>] [--blog-url <url>]

    Arguments:
      <alias>              The blog's Write.as alias (the part after write.as/).

    Options:
      -o, --output <file>  File to save the HTML to. Defaults to <alias>-archive.html.
      --blog-url <url>     Base URL for post links, e.g. a custom domain.
                           Defaults to https://write.as/<alias>/.
      -h, --help           Show this help.
    """;

string? alias = null;
string? outputPath = null;
string? blogUrl = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h" or "--help" or "/?":
            Console.WriteLine(Usage);
            return 0;
        case "-o" or "--output" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "--blog-url" when i + 1 < args.Length:
            blogUrl = args[++i];
            break;
        case var arg when !arg.StartsWith('-') && alias is null:
            alias = arg;
            break;
        default:
            Console.Error.WriteLine($"Unrecognized or incomplete argument: {args[i]}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            return 1;
    }
}

if (string.IsNullOrWhiteSpace(alias))
{
    Console.Error.WriteLine(Usage);
    return 1;
}

outputPath ??= $"{alias}-archive.html";

// The API key comes from user secrets (stored outside the repo), and the WRITEAS_API_KEY
// environment variable overrides it.
IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .Build();

string? apiKey = Environment.GetEnvironmentVariable("WRITEAS_API_KEY");
string? apiKeySource = "the WRITEAS_API_KEY environment variable";
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = configuration["WriteAs:ApiKey"];
    apiKeySource = "user secrets";
}
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = null;
    apiKeySource = null;
}

// Status messages go to stderr so stdout contains only the generated HTML.
Console.Error.WriteLine(apiKeySource is null
    ? "No Write.as API key found. Requests will be anonymous and heavily rate limited."
    : $"Using Write.as API key from {apiKeySource}.");
Console.Error.WriteLine($"Fetching posts for '{alias}' from Write.as...");

ArchiveResult result;
try
{
    using var client = new WriteAsClient(ArchiveGenerator.DefaultWriteAsUrl, apiKey);
    var progress = new Progress<string>(message => Console.Error.WriteLine($"  {message}"));
    result = await new ArchiveGenerator(client).GetItemsAsync(alias, blogUrl, progress);
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Could not reach Write.as: {ex.Message}");
    return 2;
}

if (result.Items.Count == 0)
{
    Console.Error.WriteLine($"No posts were returned for '{alias}'.");
    Console.Error.WriteLine(await WriteAsDiagnostics.ExplainNoPostsAsync(alias));
    return 2;
}

string html = ArchivePageBuilder.Build(result.Items);
await File.WriteAllTextAsync(outputPath, html);

Console.WriteLine(html);
Console.Error.WriteLine($"Found {result.Items.Count} posts. Saved to {Path.GetFullPath(outputPath)}");

if (!result.IsComplete)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"WARNING: The blog has {result.ExpectedTotal} posts but only {result.Items.Count} were fetched. The archive is incomplete.");
    Console.Error.WriteLine(WriteAsDiagnostics.RateLimitedMessage);
    return 3;
}

Console.Error.WriteLine("Paste the HTML into a Write.as page and publish it.");
return 0;
