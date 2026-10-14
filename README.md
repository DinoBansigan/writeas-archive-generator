# Write.as Archive Page Generator

A small command-line tool that builds an **archive page** for a [Write.as](https://write.as) blog: every public post, newest first, grouped by year, with the date and title linked to each post. You paste the generated HTML into a Write.as page, and that page becomes your blog's archive.

It replaces the old Blazor generator at apps.dinobansigan.com. That generator ran in the browser, which can't call the Write.as API directly because of CORS, so it needed a hosted backend API. This tool runs on your own machine and calls the Write.as API directly, so there's no CORS problem, no server and no hosting cost.

Example output:

```html
<div class="archivePage">
<h1>2026</h1>
<hr class="archivePageHr"/>
<div><a href="https://write.as/yourblog/some-post" target="_blank"><span class="archivePageDateSpan">2026-07-28:</span> Some Post</a></div>
...
</div>
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Write.as API key (optional)

The tool works without an API key, but Write.as rate limits anonymous requests heavily (see [Rate limiting](#rate-limiting)). If you have a key, store it with .NET user secrets:

```
dotnet user-secrets set "WriteAs:ApiKey" "your-key" --project src/WriteAsArchiveGenerator.Cli
```

You only need to do this once. The key is saved in your Windows user profile, at `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, outside this repository, so it can't be committed to git. The `<UserSecretsId>` is the GUID in `src/WriteAsArchiveGenerator.Cli/WriteAsArchiveGenerator.Cli.csproj`. That ID isn't secret and is meant to be committed.

When the tool runs, it says where it found the key, without ever printing the key itself:

```
Using Write.as API key from user secrets.
```

Other useful commands:

```
dotnet user-secrets list   --project src/WriteAsArchiveGenerator.Cli    # check what's stored
dotnet user-secrets remove "WriteAs:ApiKey" --project src/WriteAsArchiveGenerator.Cli
```

Note that `dotnet user-secrets list` prints the key in plain text, so don't run it while sharing your screen.

You can also provide the key in the `WRITEAS_API_KEY` environment variable, for example when running in CI. If both are set, the environment variable is used:

```powershell
$env:WRITEAS_API_KEY = "your-key"   # applies to the current terminal session only
```

Don't pass the key as a command-line argument or put it in a file inside the repository.

## Usage

From the repository folder:

```
dotnet run --project src/WriteAsArchiveGenerator.Cli -- <alias>
```

`<alias>` is your blog's Write.as alias, the part after `write.as/` in your blog's address. For `https://write.as/myblog/`, the alias is `myblog`.

The tool:

1. Fetches every public post from your blog. Progress is shown as it goes.
2. Saves the archive HTML to `<alias>-archive.html` in the current folder.
3. Prints the same HTML to the console.

Then, to publish it:

1. Open the file, select everything and copy it.
2. In Write.as, create a new page (or edit your existing archive page), paste the HTML in, and publish.

### Options

| Option | Description |
| --- | --- |
| `-o`, `--output <file>` | Save the HTML to a different file. |
| `--blog-url <url>` | Base URL for post links. Use this if your blog is on a custom domain, e.g. `--blog-url https://example.com/`. Defaults to `https://write.as/<alias>/`. |
| `-h`, `--help` | Show help. |

Examples:

```
dotnet run --project src/WriteAsArchiveGenerator.Cli -- myblog -o archive.html
dotnet run --project src/WriteAsArchiveGenerator.Cli -- myblog --blog-url https://example.com/
```

Status messages go to stderr and only the HTML goes to stdout, so `> archive.html` or `| clip` captures just the HTML.

### Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. All posts were fetched. |
| 1 | Bad or missing arguments. |
| 2 | No posts were fetched. The message explains why (wrong alias, private blog, rate limited, network error). |
| 3 | Some posts were fetched but not all. The file is still saved, but the archive is incomplete, so don't publish it. Wait a while and run it again. |

## Styling the archive page

The HTML uses three CSS classes, the same ones as the old generator, so existing custom CSS keeps working. Add rules for them in your Write.as blog's **Customize → Custom CSS**:

- `archivePage` wraps the whole archive.
- `archivePageHr` is the line under each year heading.
- `archivePageDateSpan` is the date in front of each post title.

For example:

```css
.archivePageDateSpan { font-family: monospace; color: #888; }
.archivePageHr { margin-bottom: 1em; }
```

## How titles are chosen

- Posts with a title use it as-is.
- Untitled posts use the first eight words of the post body, with Markdown formatting stripped, followed by `...` if the body is longer. A post with no text at all is shown as "Untitled Post".
- HTML characters in titles are encoded. `#` is written as `&#35;` so Write.as doesn't turn words in titles into hashtag links.

Dates are shown in your computer's local time zone.

## Rate limiting

Write.as rate limits its posts API heavily, and the tool can only fetch 10 posts per request. To cope with this, the tool:

- Waits 10 seconds between page requests.
- Retries a request that still gets rate limited, waiting 5, 15, 30 and then 60 seconds.
- Checks the number of posts it got against the number your blog reports. If any are missing, it warns you and exits with code 3 instead of quietly saving an incomplete archive.

Because of the 10-second wait, a run takes at least 10 seconds for every 10 posts, so about a minute and a half for 100 posts, and longer if retries are needed. If you have a Write.as API key, [set it up](#writeas-api-key-optional) so the tool sends it with each request.

## Project layout

```
src/WriteAsArchiveGenerator.Core    Fetching posts, generating titles, building the HTML (reusable library)
src/WriteAsArchiveGenerator.Cli     The command-line app
tests/WriteAsArchiveGenerator.Core.Tests
```

All the logic lives in `Core`, so a future desktop UI can reuse it without changes. It depends on:

- [WriteAs.NET](https://github.com/DinoBansigan/WriteAs.NET) for calling the Write.as API.
- [Markdig](https://github.com/xoofx/markdig) for turning post Markdown into plain text for untitled-post titles. It replaces CommonMark.NET and HtmlAgilityPack from the old version.

Run the tests with:

```
dotnet test
```

## Building a standalone executable

To get a single `.exe` that runs without the .NET SDK installed:

```
dotnet publish src/WriteAsArchiveGenerator.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Then run `publish\WriteAsArchiveGenerator.exe <alias>`.

The executable reads the same user secrets as `dotnet run`, as long as it runs under the same Windows user account. On another machine, set the key there with `dotnet user-secrets`, or use the `WRITEAS_API_KEY` environment variable.

## License

[MIT](LICENSE)
