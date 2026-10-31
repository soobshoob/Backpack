using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Updates;

/// <summary>Версия SemVer 2.0 (major.minor.patch[-pre]); метаданные сборки после «+» отбрасываются.</summary>
public sealed partial record SemVersion(int Major, int Minor, int Patch, string? Pre) : IComparable<SemVersion>
{
    public bool IsPrerelease => Pre is not null;

    public static bool TryParse(string? text, out SemVersion version)
    {
        version = new SemVersion(0, 0, 0, null);
        if (text is null)
        {
            return false;
        }

        var m = Pattern().Match(text.Trim());
        if (!m.Success)
        {
            return false;
        }

        version = new SemVersion(
            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
            m.Groups[4].Success ? m.Groups[4].Value : null);
        return true;
    }

    public int CompareTo(SemVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int c = Major.CompareTo(other.Major);
        if (c == 0)
        {
            c = Minor.CompareTo(other.Minor);
        }

        if (c == 0)
        {
            c = Patch.CompareTo(other.Patch);
        }

        if (c != 0)
        {
            return c;
        }

        if (Pre is null || other.Pre is null)
        {
            return Pre is null ? (other.Pre is null ? 0 : 1) : -1; // выпуск старше любой предварительной версии
        }

        var a = Pre.Split('.');
        var b = other.Pre.Split('.');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            bool an = int.TryParse(a[i], NumberStyles.None, CultureInfo.InvariantCulture, out int ai);
            bool bn = int.TryParse(b[i], NumberStyles.None, CultureInfo.InvariantCulture, out int bi);
            int r = an && bn ? ai.CompareTo(bi) : an ? -1 : bn ? 1 : string.CompareOrdinal(a[i], b[i]);
            if (r != 0)
            {
                return r;
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    public override string ToString() => Pre is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Pre}";

    [GeneratedRegex(@"^v?(\d{1,6})\.(\d{1,6})\.(\d{1,6})(?:-([0-9A-Za-z.-]{1,64}))?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex Pattern();
}

/// <summary>Репозиторий GitHub с выпусками. Задаётся владельцем при сборке; адрес не угадывается.</summary>
public sealed partial record UpdateSource(string Owner, string Repository)
{
    public static UpdateSource? Parse(string? ownerSlashRepo)
    {
        if (string.IsNullOrWhiteSpace(ownerSlashRepo))
        {
            return null;
        }

        var parts = ownerSlashRepo.Trim().Split('/');
        return parts.Length == 2 && NamePattern().IsMatch(parts[0]) && NamePattern().IsMatch(parts[1]) ? new UpdateSource(parts[0], parts[1]) : null;
    }

    public Uri ApiUri => new($"https://api.github.com/repos/{Owner}/{Repository}/releases?per_page=30");

    public string ReleasesPage => $"https://github.com/{Owner}/{Repository}/releases";

    /// <summary>Страница выпуска допустима, только если она на github.com в этом же репозитории (никаких сторонних ссылок).</summary>
    public bool IsOwnPage(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "github.com"
        && u.AbsolutePath.StartsWith($"/{Owner}/{Repository}/releases", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{Owner}/{Repository}";

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,100}$")]
    private static partial Regex NamePattern();
}

public enum UpdateState { NotConfigured, UpToDate, Available, NoSuitablePackage, Offline, Timeout, RateLimited, BadResponse, Failed }

/// <summary>Сведения о выпуске (без загрузки самого пакета).</summary>
public sealed record ReleaseInfo(SemVersion Version, string Tag, DateTimeOffset? Published, string Notes, string PageUrl, bool Prerelease, string? PackageName);

public sealed record UpdateCheckResult(UpdateState State, ReleaseInfo? Release = null, int? HttpStatus = null);

/// <summary>
/// Проверка GitHub Releases по явной команде или по отключаемому таймеру. Отправляется только GET к API GitHub с заголовком
/// User-Agent «KeyOverlay/версия»; настройки, профили и данные ввода не передаются. Ничего не скачивается и не запускается:
/// результат — номер, дата, описание и ссылка на официальную страницу выпуска. Stable не предлагает предварительные версии.
/// Подходящий пакет — вложение выпуска с «win-x64» в имени и расширением .zip.
/// </summary>
public sealed class UpdateChecker
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly UpdateSource? _source;
    private readonly HttpMessageHandler? _handler;
    private readonly TimeSpan _timeout;

    public UpdateChecker(UpdateSource? source, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _source = source;
        _handler = handler;
        _timeout = timeout ?? DefaultTimeout;
    }

    public UpdateSource? Source => _source;

    public async Task<UpdateCheckResult> CheckAsync(SemVersion current, UpdateChannel channel, CancellationToken ct = default)
    {
        if (_source is null)
        {
            return new UpdateCheckResult(UpdateState.NotConfigured);
        }

        using var client = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, _source.ApiUri);
        request.Headers.UserAgent.ParseAdd("KeyOverlay/" + current);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        string body;
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                && (response.StatusCode == HttpStatusCode.TooManyRequests || (response.Headers.TryGetValues("X-RateLimit-Remaining", out var rem) && rem.FirstOrDefault() == "0")))
            {
                return new UpdateCheckResult(UpdateState.RateLimited, HttpStatus: (int)response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(UpdateState.Failed, HttpStatus: (int)response.StatusCode);
            }

            if (response.Content.Headers.ContentLength is > 4 * 1024 * 1024)
            {
                return new UpdateCheckResult(UpdateState.BadResponse);
            }

            body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateState.Timeout);
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult(UpdateState.Offline);
        }

        return Evaluate(body, current, channel, _source);
    }

    /// <summary>Разбор ответа API (отдельно — для тестов без сети).</summary>
    public static UpdateCheckResult Evaluate(string json, SemVersion current, UpdateChannel channel, UpdateSource source)
    {
        List<ReleaseInfo> releases = new();
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new UpdateCheckResult(UpdateState.BadResponse);
            }

            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.ValueKind != JsonValueKind.Object || Bool(r, "draft"))
                {
                    continue;
                }

                string tag = Str(r, "tag_name") ?? "";
                if (!SemVersion.TryParse(tag, out var v))
                {
                    continue;
                }

                bool pre = Bool(r, "prerelease") || v.IsPrerelease;
                if (channel == UpdateChannel.Stable && pre)
                {
                    continue;
                }

                string? page = Str(r, "html_url");
                if (!source.IsOwnPage(page))
                {
                    continue;
                }

                string? package = null;
                if (r.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    package = assets.EnumerateArray().Select(a => Str(a, "name"))
                        .FirstOrDefault(n => n is not null && n.Contains("win-x64", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                }

                DateTimeOffset? published = DateTimeOffset.TryParse(Str(r, "published_at"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
                string notes = Str(r, "body") ?? "";
                releases.Add(new ReleaseInfo(v, tag, published, notes.Length > 4000 ? notes[..4000] + "…" : notes, page!, pre, package));
            }
        }
        catch (JsonException)
        {
            return new UpdateCheckResult(UpdateState.BadResponse);
        }

        var newest = releases.Where(r => r.Version.CompareTo(current) > 0).OrderByDescending(r => r.Version).FirstOrDefault();
        if (newest is null)
        {
            return new UpdateCheckResult(UpdateState.UpToDate);
        }

        return newest.PackageName is null ? new UpdateCheckResult(UpdateState.NoSuitablePackage, newest) : new UpdateCheckResult(UpdateState.Available, newest);
    }

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
}
