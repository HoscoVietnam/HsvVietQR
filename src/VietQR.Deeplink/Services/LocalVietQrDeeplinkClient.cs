using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VietQR.Core;
using VietQR.Deeplink.Client;

namespace VietQR.Deeplink;

/// <summary>
/// Thực thi <see cref="IVietQrDeeplinkClient"/> trực tiếp trong bộ nhớ (In-Process) khi máy chủ <c>VietQR.Deeplink</c>
/// được nhúng cùng ứng dụng (không cần gửi HTTP request vòng qua mạng).
/// </summary>
public sealed class LocalVietQrDeeplinkClient : IVietQrDeeplinkClient
{
    private readonly DeeplinkAppCatalog catalog;
    private readonly DeeplinkIntentStore intentStore;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly DeeplinkServerOptions options;

    public LocalVietQrDeeplinkClient(
        DeeplinkAppCatalog catalog,
        DeeplinkIntentStore intentStore,
        IHttpContextAccessor httpContextAccessor,
        IOptions<DeeplinkServerOptions> options)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.intentStore = intentStore ?? throw new ArgumentNullException(nameof(intentStore));
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        this.options = options?.Value ?? new DeeplinkServerOptions();
    }

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public Task<DeeplinkIntentResult?> CreateIntentAsync(VietQr qr, string appCode, CancellationToken ct = default)
    {
        if (qr is null) throw new ArgumentNullException(nameof(qr));
        return CreateIntentAsync(appCode, qr.Build(), ct);
    }

    /// <inheritdoc />
    public Task<DeeplinkIntentResult?> CreateIntentAsync(string appCode, string qrContent, CancellationToken ct = default)
    {
        var record = intentStore.CreateIntent(appCode, qrContent);
        if (record is null)
        {
            return Task.FromResult<DeeplinkIntentResult?>(null);
        }

        var baseUrl = ResolveCurrentBaseUrl();
        var result = new DeeplinkIntentResult
        {
            Url = $"{baseUrl}/p/{record.Token}",
            ExpiresInSeconds = record.RemainingSeconds
        };

        return Task.FromResult<DeeplinkIntentResult?>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeeplinkBankAppInfo>> GetSupportedAppsAsync(CancellationToken ct = default)
    {
        var baseUrl = ResolveCurrentBaseUrl();
        var apps = catalog.GetSupportedApps(baseUrl);
        return Task.FromResult(apps);
    }

    internal string ResolveCurrentBaseUrl() =>
        DeeplinkHostExtensions.ResolveRequestBaseUrl(httpContextAccessor.HttpContext, options.BaseUrl);
}

