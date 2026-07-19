using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.Updates
{
    public class GitHubUpdateService
    {
        private const string Owner = "Badlands-RP";
        private const string Repo = "CodeWalker";
        private const string ReleasesUrl = "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases?per_page=50";
        private static readonly Regex CommitRegex = new Regex(@"\b[0-9a-fA-F]{40}\b", RegexOptions.Compiled);

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var currentCommit = GetCurrentCommit();
            var releases = await GetJsonAsync<List<GitHubRelease>>(ReleasesUrl);
            var latest = (releases ?? new List<GitHubRelease>())
                .Where(r => (r != null) && !r.Draft)
                .Select(r => new { Release = r, Asset = FindReleaseZip(r) })
                .Where(r => r.Asset != null)
                .OrderByDescending(r => ParseGitHubDate(r.Release.PublishedAt ?? r.Release.CreatedAt))
                .FirstOrDefault();

            if (latest == null)
            {
                return new UpdateCheckResult
                {
                    CurrentCommit = currentCommit,
                    ErrorMessage = "No downloadable CodeWalker release zip was found on GitHub."
                };
            }

            var targetCommit = NormalizeCommit(latest.Release.TargetCommitish);
            var result = new UpdateCheckResult
            {
                CurrentCommit = currentCommit,
                ReleaseTag = latest.Release.TagName,
                ReleaseName = latest.Release.Name,
                ReleaseUrl = latest.Release.HtmlUrl,
                ReleaseBody = latest.Release.Body,
                TargetCommit = targetCommit,
                TargetCommitish = latest.Release.TargetCommitish,
                AssetName = latest.Asset.Name,
                AssetSize = latest.Asset.Size,
                AssetDownloadUrl = latest.Asset.BrowserDownloadUrl,
                IsPrerelease = latest.Release.Prerelease
            };

            if (string.IsNullOrEmpty(currentCommit))
            {
                result.IsUpdateAvailable = true;
                result.Summary = "Current build commit could not be detected, so the latest GitHub release is available to install.";
                return result;
            }

            if (string.Equals(currentCommit, targetCommit, StringComparison.OrdinalIgnoreCase))
            {
                result.IsUpdateAvailable = false;
                result.Summary = "This build already matches the latest GitHub release.";
                return result;
            }

            var compare = await GetCompareAsync(currentCommit, targetCommit);
            if (compare != null)
            {
                result.CompareStatus = compare.Status;
                result.AheadBy = compare.AheadBy;
                result.BehindBy = compare.BehindBy;
                result.TotalCommits = compare.TotalCommits;
                result.CommitChanges = BuildCommitChanges(compare.Commits);
            }

            result.IsUpdateAvailable = true;
            result.Summary = result.CommitChanges.Count > 0
                ? "Newer commits were found in the latest GitHub release."
                : "The latest GitHub release is different from this build.";

            return result;
        }

        public async Task<string> DownloadReleaseZipAsync(UpdateCheckResult update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            if (string.IsNullOrEmpty(update.AssetDownloadUrl)) throw new InvalidOperationException("The update has no download URL.");

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var tempDir = Path.Combine(Path.GetTempPath(), "BadWalkerUpdates");
            Directory.CreateDirectory(tempDir);

            var safeName = MakeSafeFileName(update.AssetName);
            if (string.IsNullOrEmpty(safeName)) safeName = "BadWalkerUpdate.zip";
            var zipPath = Path.Combine(tempDir, safeName);

            using (var client = CreateHttpClient())
            using (var response = await client.GetAsync(update.AssetDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output);
                }
            }

            return zipPath;
        }

        public void StartUpdaterAndRestart(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException("Update zip was not found.", zipPath);
            }

            var exePath = Application.ExecutablePath;
            var installDir = Path.GetDirectoryName(exePath);
            var scriptPath = Path.Combine(Path.GetTempPath(), "BadWalkerUpdater-" + Guid.NewGuid().ToString("N") + ".ps1");

            File.WriteAllText(scriptPath, BuildUpdaterScript(), Encoding.UTF8);

            var args = string.Join(" ", new[]
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", Quote(scriptPath),
                "-ProcessId", Process.GetCurrentProcess().Id.ToString(),
                "-ZipPath", Quote(zipPath),
                "-InstallDir", Quote(installDir),
                "-ExePath", Quote(exePath)
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = installDir
            });
        }

        public static string BuildDetailsText(UpdateCheckResult update)
        {
            if (update == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(update.Summary ?? "Update check complete.");
            sb.AppendLine();
            sb.AppendLine("Current build: " + FormatCommit(update.CurrentCommit));
            sb.AppendLine("Latest release: " + (update.ReleaseTag ?? "(unknown)") + " (" + FormatCommit(update.TargetCommit ?? update.TargetCommitish) + ")");
            if (update.IsPrerelease) sb.AppendLine("Release type: prerelease");
            if (!string.IsNullOrEmpty(update.AssetName)) sb.AppendLine("Download: " + update.AssetName + " (" + FormatBytes(update.AssetSize) + ")");
            if (!string.IsNullOrEmpty(update.ReleaseUrl)) sb.AppendLine("Release URL: " + update.ReleaseUrl);

            if (update.CommitChanges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Commits in the latest release that this build does not have:");
                foreach (var commit in update.CommitChanges)
                {
                    sb.AppendLine("- " + FormatCommit(commit.Sha) + " " + commit.Title);
                }
            }
            else if (!string.IsNullOrEmpty(update.ReleaseBody))
            {
                sb.AppendLine();
                sb.AppendLine("Release notes:");
                sb.AppendLine(update.ReleaseBody.Trim());
            }

            if (!string.IsNullOrEmpty(update.CompareStatus))
            {
                sb.AppendLine();
                sb.AppendLine("GitHub compare status: " + update.CompareStatus);
            }

            return sb.ToString();
        }

        private async Task<GitHubCompare> GetCompareAsync(string currentCommit, string targetCommit)
        {
            if (string.IsNullOrEmpty(currentCommit) || string.IsNullOrEmpty(targetCommit)) return null;

            var url = "https://api.github.com/repos/" + Owner + "/" + Repo + "/compare/" + currentCommit + "..." + targetCommit;
            try
            {
                return await GetJsonAsync<GitHubCompare>(url);
            }
            catch
            {
                return null;
            }
        }

        private static List<CommitChange> BuildCommitChanges(List<GitHubCommitItem> commits)
        {
            var changes = new List<CommitChange>();
            if (commits == null) return changes;

            foreach (var item in commits)
            {
                if (item == null) continue;
                var message = item.Commit?.Message ?? string.Empty;
                var title = message
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "(no commit message)";
                changes.Add(new CommitChange
                {
                    Sha = item.Sha,
                    Title = title,
                    Url = item.HtmlUrl
                });
            }

            return changes;
        }

        private static GitHubAsset FindReleaseZip(GitHubRelease release)
        {
            if (release?.Assets == null) return null;

            return release.Assets.FirstOrDefault(a =>
                (a != null) &&
                !string.IsNullOrEmpty(a.BrowserDownloadUrl) &&
                a.Name != null &&
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<T> GetJsonAsync<T>(string url)
        {
            using (var client = CreateHttpClient())
            using (var response = await client.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    return (T)serializer.ReadObject(stream);
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BadWalker-Updater");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.Timeout = TimeSpan.FromSeconds(45);
            return client;
        }

        private static string GetCurrentCommit()
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(GitHubUpdateService).Assembly;
            var info = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;

            var match = CommitRegex.Match(info ?? string.Empty);
            if (match.Success) return match.Value.ToLowerInvariant();

            return GetCurrentGitCommitFromWorkingTree();
        }

        private static string GetCurrentGitCommitFromWorkingTree()
        {
            try
            {
                var dir = Path.GetDirectoryName(Application.ExecutablePath);
                while (!string.IsNullOrEmpty(dir))
                {
                    if (Directory.Exists(Path.Combine(dir, ".git"))) break;
                    dir = Directory.GetParent(dir)?.FullName;
                }

                if (string.IsNullOrEmpty(dir)) return null;

                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = dir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null) return null;
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(3000);
                    return CommitRegex.IsMatch(output) ? output.ToLowerInvariant() : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeCommit(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var match = CommitRegex.Match(value);
            if (match.Success) return match.Value.ToLowerInvariant();

            return value;
        }

        private static DateTimeOffset ParseGitHubDate(string value)
        {
            DateTimeOffset date;
            return DateTimeOffset.TryParse(value, out date) ? date : DateTimeOffset.MinValue;
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value;
        }

        private static string FormatCommit(string commit)
        {
            if (string.IsNullOrEmpty(commit)) return "(unknown)";
            return commit.Length > 7 ? commit.Substring(0, 7) : commit;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "unknown size";
            if (bytes < 1024) return bytes.ToString() + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string BuildUpdaterScript()
        {
            return @"
param(
    [int]$ProcessId,
    [string]$ZipPath,
    [string]$InstallDir,
    [string]$ExePath
)

$ErrorActionPreference = 'Stop'

try {
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($process) {
        if (-not $process.WaitForExit(600000)) {
            exit 1
        }
    }
} catch { }

$extractDir = Join-Path ([System.IO.Path]::GetTempPath()) ('BadWalkerUpdate_' + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extractDir | Out-Null

Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractDir -Force
Copy-Item -Path (Join-Path $extractDir '*') -Destination $InstallDir -Recurse -Force

Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue

Start-Process -FilePath $ExePath -ArgumentList 'project' -WorkingDirectory $InstallDir
";
        }
    }

    public class UpdateCheckResult
    {
        public string CurrentCommit { get; set; }
        public string TargetCommit { get; set; }
        public string TargetCommitish { get; set; }
        public string ReleaseTag { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseUrl { get; set; }
        public string ReleaseBody { get; set; }
        public bool IsPrerelease { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string Summary { get; set; }
        public string ErrorMessage { get; set; }
        public string CompareStatus { get; set; }
        public int AheadBy { get; set; }
        public int BehindBy { get; set; }
        public int TotalCommits { get; set; }
        public string AssetName { get; set; }
        public long AssetSize { get; set; }
        public string AssetDownloadUrl { get; set; }
        public List<CommitChange> CommitChanges { get; set; } = new List<CommitChange>();
    }

    public class CommitChange
    {
        public string Sha { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
    }

    [DataContract]
    internal class GitHubRelease
    {
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "html_url")]
        public string HtmlUrl { get; set; }

        [DataMember(Name = "body")]
        public string Body { get; set; }

        [DataMember(Name = "target_commitish")]
        public string TargetCommitish { get; set; }

        [DataMember(Name = "draft")]
        public bool Draft { get; set; }

        [DataMember(Name = "prerelease")]
        public bool Prerelease { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "published_at")]
        public string PublishedAt { get; set; }

        [DataMember(Name = "assets")]
        public List<GitHubAsset> Assets { get; set; }
    }

    [DataContract]
    internal class GitHubAsset
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "size")]
        public long Size { get; set; }

        [DataMember(Name = "browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }

    [DataContract]
    internal class GitHubCompare
    {
        [DataMember(Name = "status")]
        public string Status { get; set; }

        [DataMember(Name = "ahead_by")]
        public int AheadBy { get; set; }

        [DataMember(Name = "behind_by")]
        public int BehindBy { get; set; }

        [DataMember(Name = "total_commits")]
        public int TotalCommits { get; set; }

        [DataMember(Name = "commits")]
        public List<GitHubCommitItem> Commits { get; set; }
    }

    [DataContract]
    internal class GitHubCommitItem
    {
        [DataMember(Name = "sha")]
        public string Sha { get; set; }

        [DataMember(Name = "html_url")]
        public string HtmlUrl { get; set; }

        [DataMember(Name = "commit")]
        public GitHubCommit Commit { get; set; }
    }

    [DataContract]
    internal class GitHubCommit
    {
        [DataMember(Name = "message")]
        public string Message { get; set; }
    }
}
