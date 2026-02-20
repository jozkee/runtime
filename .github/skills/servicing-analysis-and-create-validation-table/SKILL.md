---
name: servicing-analysis-and-create-validation-table
description: Analyze servicing PRs from release.dot.net for dotnet/dotnet and dotnet/runtime, filter by file changes, and create a validation table.
---

# Servicing Analysis and Create Validation Table

## When to Use This Skill

- User asks about servicing PRs for a .NET release (8.0.x, 9.0.x, 10.0.x)
- User needs a validation table of runtime changes going into a servicing release
- User wants to know what dotnet/dotnet and dotnet/runtime PRs are included in upcoming patches

## Overview

This skill scrapes PR data from https://release.dot.net/payload-tracking, then filters and analyzes the PRs to produce a validation table. The workflow has two phases:

1. **Data collection**: Run the script to fetch PR lists from the release tracker (requires Edge with active login session)
2. **PR analysis and filtering**: Use `gh` CLI or GitHub API to fetch changed files for each PR, apply filtering rules, and build the validation table

## Step 1: Run the Data Collection Script

The script is a file-based C# program (no `.csproj` needed). It requires a globally installed .NET 10+ SDK.

Run the script:

```powershell
dotnet -f net10.0 <repo_root>/.github/skills/servicing-analysis-and-create-validation-table/scripts/Get-ServicingPRs.cs
```

### What the Script Does

1. Opens Edge with the user's profile (copies to temp dir to avoid conflicts)
2. Navigates to https://release.dot.net/ and scrapes current servicing versions for 10.0.x, 9.0.x, 8.0.x (excludes previews)
3. For each version, navigates to https://release.dot.net/payload-tracking, selects the version, clicks "Get PRs"
4. Fetches all PRs in parallel (one browser tab per version)
5. Filters to only `dotnet/dotnet` and `dotnet/runtime` repositories
6. Outputs structured JSON to stdout

### Script Output Format

```json
{
  "10.0.4": [
    {
      "repository": "dotnet/runtime",
      "pullRequestNumber": 124223,
      "title": "[release/10.0] fix Vector2/3 EqualsAny",
      "status": "Merged",
      "pullRequestUrl": "https://github.com/dotnet/runtime/pull/124223"
    }
  ],
  "9.0.14": [...],
  "8.0.25": [...]
}
```

### Prerequisites

- Globally installed .NET 10+ SDK (`dotnet --version` should return 10.0.x or later)
- Microsoft Edge installed with an active login session to release.dot.net

## Step 2: Fetch Changed Files for Each PR

For each PR in the script output, fetch the list of changed files.

### For GitHub PRs (github.com URLs)

Use `gh` CLI if available:

```powershell
gh pr view <PR_NUMBER> --repo dotnet/runtime --json files --jq '.files[].path'
gh pr view <PR_NUMBER> --repo dotnet/dotnet --json files --jq '.files[].path'
```

If `gh` is not available, use the GitHub API:

```
GET https://api.github.com/repos/dotnet/runtime/pulls/<PR_NUMBER>/files
GET https://api.github.com/repos/dotnet/dotnet/pulls/<PR_NUMBER>/files
```

### For AzDO PRs (dev.azure.com URLs)

AzDO PRs (from dotnet/dotnet internal) typically mirror GitHub PRs. Try to find the corresponding GitHub PR by title or PR number. If not possible, include the PR in the results unfiltered.

## Step 3: Apply Filtering Rules

Apply these rules **in order** to discard irrelevant PRs:

### Rule 1: dotnet/dotnet — Keep only PRs touching `src/runtime`

For PRs from `dotnet/dotnet`, check if **any** changed file has a path starting with `src/runtime/`. If none do, **discard** the PR.

### Rule 2: All PRs — Discard eng/test-only changes

For all remaining PRs (from both `dotnet/dotnet` and `dotnet/runtime`), check whether **every** changed file matches one of these patterns:

- `eng/**` (infrastructure files)
- `**/*.Tests/**` or `**/tests/**` or `**/test/**` (test files)
- `.github/**`
- `.config/**`
- `.devcontainer/**`
- `*.md` (documentation)

If **all** files match these patterns (i.e., no product source code is touched), **discard** the PR.

## Step 4: Build the Validation Table

For each version, produce a validation document using this template. Replace placeholders with actual data.

### Template

```
<MONTH> <YEAR> Release — <VERSION>
Due date for sign-offs: <1 DAY BEFORE CTI SIGNOFF DEADLINE FROM CALENDAR>

• The fixes introduced in this build need to be verified using a repro app, or, if that is not possible, verify the DLL contents directly.
  You can follow these instructions: [Using the feed](onenote:#Using%20the%20feed)
• Any infra or test related changes can be skipped if you think there is nothing to verify.
• If you find any issues, please describe them in the Comments column.
• If all looks good, please write "Validated" in the Validation Status column.

• All artifacts and NuGet feed info can be found at: https://release.dot.net/

• The runtime nuget packages can be found under:
    "repos -> runtime-<GUID> -> shipping -> packages"

• The shared framework installers for all platforms (and symbols) can be found under:
    "shipping -> assets -> Runtime -> X.X.X-servicing.XXXXX.X"

• All the nuget packages (including the runtime packages) can be found under:
    "shipping -> packages"

• All the SDK installers can be found in:
    "shipping -> assets -> Sdk -> X.X.X-servicing.X"

| PR | Author/Reviewers | Comments | Validation Status |
|----|-----------------|----------|-------------------|
| [#124223 — fix Vector2/3 EqualsAny](https://github.com/dotnet/runtime/pull/124223) | [tannergooding](mailto:tagoo@microsoft.com) / [artl93](mailto:artl@microsoft.com), [jeffhandley](mailto:jeffhand@microsoft.com) | | |
| ... | ... | | |

Teams conversation participants (copy-paste to create a Teams chat):
alias1@microsoft.com; alias2@microsoft.com; alias3@microsoft.com
```

## Step 5: Generate the Teams Group Chat Link

After producing all per-version validation documents, generate a **single** Teams deep link that aggregates participants from **all** versions. This link starts a group chat with every person who has a PR across any of the analyzed versions.

### Template

```
Start a Teams group chat:
https://teams.microsoft.com/l/chat/0/0?users=<COMMA_SEPARATED_EMAILS>&topicName=<MONTH>%20<YEAR>%20validation&message=Hi%20all!%20We%20have%20changes%20in%20the%20<MONTH>%20<YEAR>%20servicing%20releases%20that%20need%20your%20validation%20sign-off%20by%20end-of-day%20<DEADLINE>.%20Please%20review%20the%20PRs%20assigned%20to%20you%20and%20verify%20the%20fixes.%20Feed%20and%20artifact%20links%20are%20available%20at%20https%3A%2F%2Frelease.dot.net%2F%20%E2%80%94%20thank%20you!
```

### How to Fill the Link

- `<COMMA_SEPARATED_EMAILS>`: Collect all unique `@microsoft.com` emails from the Author/Reviewers columns across **every** version's validation table. Deduplicate and join with commas (no spaces). Example: `tagoo@microsoft.com,artl@microsoft.com,ericstj@microsoft.com`
- `<MONTH>` and `<YEAR>`: The current month and year (e.g., `February` and `2026`). URL-encode spaces in the `topicName` parameter (e.g., `February%202026%20validation`).
- `<DEADLINE>`: The sign-off deadline date. URL-encode it in the message.
- The message text should be fully URL-encoded. Replace `<MONTH>`, `<YEAR>`, and `<DEADLINE>` with actual values before URL-encoding.

### How to Fill the Template

- **`<MONTH> <YEAR>`**: Use the current month and year (e.g., "February 2026")
- **`<VERSION>`**: The servicing version (e.g., "10.0.4")
- **Due date**: Leave the placeholder as-is for a human to fill in from the CTI calendar
- **PR column**: Format as `[#<number> — <clean title>](<url>)`. Strip the repository prefix (e.g., `dotnet/runtime#124223`) and any `[release/X.0]` prefix from the title — keep just the issue number and the descriptive part of the title.
- **Author/Reviewers**: Use `mailto:` hyperlinks for GitHub handles when an email is available in the mapping file: `[handle](mailto:alias@microsoft.com)`. If no email is available, use the plain handle. Fetch data from GitHub using `gh` CLI or API:

```powershell
# Get PR author
gh pr view <PR_NUMBER> --repo dotnet/runtime --json author --jq '.author.login'

# If author is "app/github-actions", look for the real author in the PR body.
# Servicing backport PRs created by GitHub Actions include "/cc @username" in the description.
gh pr view <PR_NUMBER> --repo dotnet/runtime --json body --jq '.body' | Select-String -Pattern '/cc @(\w+)' | ForEach-Object { $_.Matches.Groups[1].Value }

# Get ALL PR reviewers (not just approvals — include anyone who reviewed)
gh pr view <PR_NUMBER> --repo dotnet/runtime --json reviews --jq '[.reviews[].author.login] | unique | join(", ")'
```

Or via GitHub API:

```
GET https://api.github.com/repos/dotnet/runtime/pulls/<PR_NUMBER>
  → .user.login (author)
  → If author is "github-actions[bot]", parse .body for "/cc @<username>" to find the real author
GET https://api.github.com/repos/dotnet/runtime/pulls/<PR_NUMBER>/reviews
  → collect ALL unique .user.login values (all review states, not just APPROVED)
```

Format as: `[author](mailto:alias@microsoft.com) / [reviewer1](mailto:alias@microsoft.com), [reviewer2](mailto:alias@microsoft.com)`

**Note on bot authors**: Many servicing PRs are auto-created by `github-actions` (via the backport bot). The actual human author who triggered the backport is mentioned in the PR description as `/cc @username`. Use that username instead of `@app/github-actions`.

### Resolving GitHub Handles to Microsoft Emails

A mapping file at `<repo_root>/.github/skills/servicing-analysis-and-create-validation-table/github-to-email.json` maps known GitHub handles to Microsoft email addresses.

When building the validation table, look up each GitHub handle in this file. If found (and non-null), render it as a `mailto:` hyperlink: `[handle](mailto:alias@microsoft.com)`. If the email is null/unknown, use the plain handle text without a link.

If a handle is not in the mapping file, try to resolve it using these fallback methods (in order):

1. **GitHub profile**: `gh api /users/<handle> --jq '.email'` — use if it ends in `@microsoft.com`
2. **Git commit history**: `git log --all --author="<display_name>" --format="%ae" -1` — use if it ends in `@microsoft.com`
3. If neither works, use `@handle` without an email and add the handle with `null` to the mapping file for future manual resolution

**Keep the mapping file updated**: When you successfully resolve a new handle, add it to the JSON file so future runs don't need to re-resolve it.

- **Comments**: Leave blank for human validation
- **Validation Status**: Leave blank for human validation
- **Teams conversation participants**: Collect all unique Microsoft email addresses from the Author/Approvers column across all PRs in that version. Deduplicate them and emit a single semicolon-separated line at the bottom of the document. Only include `@microsoft.com` emails — skip handles that could not be resolved. This line should be ready to copy-paste directly into the "To" field when creating a new Teams group chat.
- **Teams deep link**: See Step 5 — the Teams group chat link is generated once across all versions, not per-version.

## Tips

1. The script opens a visible Edge window — do not close it while the script is running
2. The script takes ~30-60 seconds to complete (parallel page loads)
3. If the script fails, ensure Edge is not running in the foreground with the same profile
4. For large PR file lists, paginate the GitHub API (`per_page=100&page=N`)
5. AzDO PRs that cannot be resolved to GitHub PRs should be included with a note
6. The version list is dynamic — the script reads whatever versions are currently on release.dot.net
