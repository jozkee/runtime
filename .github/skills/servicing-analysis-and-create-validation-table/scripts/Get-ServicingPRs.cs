// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning disable IL2026 // Members annotated with RequiresUnreferencedCodeAttribute
#pragma warning disable IL3050 // Members annotated with RequiresDynamicCodeAttribute

#:package Microsoft.Playwright@1.58.0
// Playwirght relies reflection-based serialization.
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

var releaseBaseUrl = "https://release.dot.net/";
var payloadTrackingUrl = "https://release.dot.net/payload-tracking";
var analyzeBatchUrlSubstring = "/api/payload-tracking/analyze-batch";
var versionPrefixes = new[] { "10.0.", "9.0.", "8.0." };
var keepRepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "dotnet/dotnet", "dotnet/runtime"
};

var edgeUserDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Microsoft", "Edge", "User Data");
var tempUserDataDir = Path.Combine(Path.GetTempPath(), "servicing-analysis-edge-profile");

CopyEdgeProfile(edgeUserDataDir, tempUserDataDir);

using var playwright = await Playwright.CreateAsync();
await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
    tempUserDataDir,
    new BrowserTypeLaunchPersistentContextOptions
    {
        Channel = "msedge",
        Headless = false,
    });

var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();

page.Request += async (_, request) =>
{
    try
    {
        if (request.Url.Contains(analyzeBatchUrlSubstring, StringComparison.OrdinalIgnoreCase))
        {
            var headers = await request.AllHeadersAsync();
            if (headers.TryGetValue("authorization", out var auth))
                Console.Error.WriteLine("Bearer token captured (available for AzDO API calls if needed).");
        }
    }
    catch (PlaywrightException) { }
};

// Step 1: Scrape versions from the main release page.
await page.GotoAsync(releaseBaseUrl);
await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

// Wait for version cards to render (Blazor SPA).
var versionCards = page.Locator("h6.rz-text-h6");
await versionCards.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
var allVersionTexts = await versionCards.AllInnerTextsAsync();
var versions = allVersionTexts
    .Select(v => v.Trim())
    .Where(v => versionPrefixes.Any(p => v.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                && !v.Contains("preview", StringComparison.OrdinalIgnoreCase))
    .Distinct()
    .ToList();

Console.Error.WriteLine($"Found versions: {string.Join(", ", versions)}");

// Step 2: For each version, open a new page and get PRs in parallel.
var tasks = versions.Select(async version =>
{
    var versionPage = await context.NewPageAsync();
    try
    {
        await versionPage.GotoAsync(payloadTrackingUrl);
        await versionPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var dropdown = versionPage.Locator(".rz-dropdown").First;
        await dropdown.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        await dropdown.ClickAsync();
        var option = versionPage.Locator(".rz-dropdown-items-wrapper").GetByText(version, new() { Exact = true });
        await option.ClickAsync();

        var getPrsButton = versionPage.GetByText("Get PRs");
        await getPrsButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });

        var responseTask = versionPage.WaitForResponseAsync(
            resp => resp.Url.Contains(analyzeBatchUrlSubstring, StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 120_000 });

        await getPrsButton.ClickAsync();

        var response = await responseTask;
        var json = await response.TextAsync();
        return (version, status: response.Status, json);
    }
    finally
    {
        await versionPage.CloseAsync();
    }
}).ToList();

var results = await Task.WhenAll(tasks);

// Step 3: Parse and output JSON with repo filtering applied.
var output = new Dictionary<string, List<object>>();

foreach (var (version, status, json) in results)
{
    var prList = new List<object>();
    using var doc = JsonDocument.Parse(json);

    if (doc.RootElement.TryGetProperty("results", out var apiResults) && apiResults.ValueKind == JsonValueKind.Array)
    {
        foreach (var res in apiResults.EnumerateArray())
        {
            if (!res.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
                continue;
            if (!res.TryGetProperty("result", out var resultObj))
                continue;
            if (!resultObj.TryGetProperty("pullRequests", out var prs) || prs.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var pr in prs.EnumerateArray())
            {
                var repo = pr.TryGetProperty("repository", out var r) ? r.GetString() : null;
                if (repo == null || !keepRepos.Contains(repo)) continue;

                var num = pr.TryGetProperty("pullRequestNumber", out var n) ? n.GetInt32() : 0;
                var title = pr.TryGetProperty("title", out var t) ? t.GetString() : "";
                var prStatus = pr.TryGetProperty("status", out var s) ? s.GetString() : "";
                var url = pr.TryGetProperty("pullRequestUrl", out var u) ? u.GetString() : "";

                prList.Add(new { repository = repo, pullRequestNumber = num, title, status = prStatus, pullRequestUrl = url });
            }
        }
    }

    output[version] = prList;
}

// Output structured JSON to stdout for the agent to consume.
Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));

// --- Helper functions ---

void CopyEdgeProfile(string source, string destination)
{
    var filesToCopy = new[] { "Local State" };
    var subDirsToCopy = new[] { "Default", "Profile 1", "Profile 2" };
    var skipSubDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Cache", "Code Cache", "GPUCache", "Service Worker",
        "ScriptCache", "DawnGraphiteCache", "DawnWebGPUCache"
    };

    if (Directory.Exists(destination))
        Directory.Delete(destination, true);
    Directory.CreateDirectory(destination);

    foreach (var file in filesToCopy)
    {
        var src = Path.Combine(source, file);
        if (File.Exists(src))
            File.Copy(src, Path.Combine(destination, file), true);
    }

    foreach (var subDir in subDirsToCopy)
    {
        var srcDir = Path.Combine(source, subDir);
        if (!Directory.Exists(srcDir))
            continue;
        CopyDirFiltered(srcDir, Path.Combine(destination, subDir), skipSubDirs);
    }
}

void CopyDirFiltered(string sourceDir, string destDir, HashSet<string> skipDirs)
{
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir))
    {
        try { File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true); }
        catch (IOException) { }
    }
    foreach (var dir in Directory.GetDirectories(sourceDir))
    {
        var dirName = Path.GetFileName(dir);
        if (skipDirs.Contains(dirName)) continue;
        CopyDirFiltered(dir, Path.Combine(destDir, dirName), skipDirs);
    }
}
