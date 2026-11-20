using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VietQR.Core;
using VietQR.Deeplink.Client;

namespace VietQR.Deeplink;

/// <summary>
/// Các phương thức mở rộng để đăng ký và kích hoạt máy chủ điều hướng VietQR Deeplink (<c>VietQR.Deeplink</c>)
/// trên nền tảng ASP.NET Core.
/// </summary>
public static class DeeplinkHostExtensions
{
    /// <summary>
    /// Đăng ký các dịch vụ lõi của máy chủ VietQR Deeplink (Catalog 44+ ứng dụng, Intent Store, và Local In-Process Client).
    /// </summary>
    public static IServiceCollection AddDeeplinkServer(
        this IServiceCollection services,
        IConfiguration? config = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        if (config is not null)
        {
            services.Configure<DeeplinkServerOptions>(config.GetSection(DeeplinkServerOptions.SectionName));
        }
        else
        {
            services.AddOptions<DeeplinkServerOptions>();
        }

        services.AddHttpContextAccessor();
        services.AddSingleton(DeeplinkAppCatalog.Instance);
        services.AddSingleton<DeeplinkIntentStore>();

        // Ghi đè IVietQrDeeplinkClient bằng LocalVietQrDeeplinkClient để các lời gọi nội bộ (như DeeplinkController)
        // thực thi trực tiếp trong bộ nhớ RAM mà không cần vòng qua mạng.
        services.AddSingleton<IVietQrDeeplinkClient, LocalVietQrDeeplinkClient>();

        return services;
    }

    /// <summary>
    /// Kích hoạt các Endpoint của máy chủ VietQR Deeplink:
    /// <list type="bullet">
    /// <item><description><c>GET /v1/apps</c>: Danh sách 44+ ứng dụng ngân hàng &amp; ví điện tử.</description></item>
    /// <item><description><c>POST /v1/intents</c>: Khởi tạo phiên điều hướng ngắn hạn.</description></item>
    /// <item><description><c>GET /p/{token}</c>: Trang cầu nối thanh toán thông minh (Smart Payment Bridge).</description></item>
    /// <item><description><c>GET /p/{token}/qr.png</c>: Ảnh mã VietQR của phiên điều hướng.</description></item>
    /// </list>
    /// </summary>
    public static WebApplication UseDeeplinkServer(this WebApplication app)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));

        // 1. Endpoint lấy danh sách 44+ ứng dụng hỗ trợ (dành cho VietQR.Deeplink.Client kết nối từ xa)
        app.MapGet("/v1/apps", (HttpContext ctx, DeeplinkAppCatalog catalog, IOptions<DeeplinkServerOptions> opts) =>
        {
            var baseUrl = ResolveRequestBaseUrl(ctx, opts.Value.BaseUrl);
            var apps = catalog.GetSupportedApps(baseUrl);
            return Results.Json(new
            {
                code = "00",
                desc = "success",
                data = apps
            });
        }).ExcludeFromDescription();

        // 2. Endpoint tạo phiên điều hướng Intent (dành cho VietQR.Deeplink.Client kết nối từ xa)
        app.MapPost("/v1/intents", (
            HttpContext ctx,
            GatewayCreateIntentPayload payload,
            DeeplinkIntentStore intentStore,
            IOptions<DeeplinkServerOptions> opts) =>
        {
            var qrRaw = !string.IsNullOrWhiteSpace(payload.QrContent) ? payload.QrContent : payload.QrCode;
            if (string.IsNullOrWhiteSpace(payload.AppCode) ||
                string.IsNullOrWhiteSpace(qrRaw) ||
                !VietQr.VerifyCRC(qrRaw.Trim()))
            {
                return Results.BadRequest(new
                {
                    code = "02",
                    desc = "Chuỗi VietQR hoặc mã ứng dụng không hợp lệ.",
                    data = (object?)null
                });
            }

            var record = intentStore.CreateIntent(payload.AppCode.Trim(), qrRaw.Trim());
            if (record is null)
            {
                return Results.NotFound(new
                {
                    code = "04",
                    desc = "Không hỗ trợ mở ứng dụng này.",
                    data = (object?)null
                });
            }

            var baseUrl = ResolveRequestBaseUrl(ctx, opts.Value.BaseUrl);
            ctx.Response.Headers.CacheControl = "no-store";

            return Results.Json(new
            {
                code = "00",
                desc = "success",
                data = new DeeplinkIntentResult
                {
                    Url = $"{baseUrl}/p/{record.Token}",
                    ExpiresInSeconds = record.RemainingSeconds
                }
            });
        }).ExcludeFromDescription();

        // 3. Trang cầu nối điều hướng thông minh GET /p/{token}
        app.MapGet("/p/{token}", (
            string token,
            HttpContext ctx,
            DeeplinkIntentStore intentStore,
            IOptions<DeeplinkServerOptions> opts) =>
        {
            ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

            var record = intentStore.ResolveIntent(token, ctx.Request.Query);
            if (record is null)
            {
                return Results.Content(
                    DeeplinkBridgePageRenderer.RenderExpiredPageHtml(),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status404NotFound);
            }

            // Nếu truyền ?mode=redirect thì chuyển hướng HTTP 302 trực tiếp sang đường dẫn Autofill
            var mode = ctx.Request.Query["mode"].FirstOrDefault();
            if (string.Equals(mode, "redirect", StringComparison.OrdinalIgnoreCase))
            {
                var redirectUrl = DeeplinkBridgePageRenderer.BuildAutofillUrl(record, opts.Value.AutofillGatewayUrl);
                return Results.Redirect(redirectUrl, permanent: false);
            }

            var baseUrl = ResolveRequestBaseUrl(ctx, opts.Value.BaseUrl);
            var userAgent = ctx.Request.Headers.UserAgent.FirstOrDefault();
            var html = DeeplinkBridgePageRenderer.RenderPaymentBridgeHtml(
                record,
                userAgent,
                baseUrl,
                opts.Value.AutofillGatewayUrl);

            return Results.Content(html, "text/html; charset=utf-8");
        }).ExcludeFromDescription();

        // 4. Endpoint xuất ảnh mã VietQR trực tiếp cho trang Bridge (/p/{token}/qr.png)
        app.MapGet("/p/{token}/qr.png", async (
            string token,
            HttpContext ctx,
            DeeplinkIntentStore intentStore,
            QrImageService qrImageService,
            CancellationToken ct) =>
        {
            var record = intentStore.ResolveIntent(token, ctx.Request.Query);
            if (record is null)
            {
                return Results.NotFound();
            }

            long? amountLong = record.Amount.HasValue && record.Amount.Value > 0
                ? Convert.ToInt64(Math.Round(record.Amount.Value, 0, MidpointRounding.AwayFromZero))
                : null;

            var pngBytes = await qrImageService.RenderBytesAsync(record.QrContent, new QrRenderOptions
            {
                Template = "compact",
                Format = QrImageFormat.Png,
                BankBin = record.ReceiverBankBin,
                AccountNo = record.AccountNo,
                AccountName = record.AccountName,
                Amount = amountLong,
                Description = record.Description,
                ServiceType = record.ServiceType
            }, ct);

            if (ctx.Request.Query.ContainsKey("download"))
            {
                ctx.Response.Headers.ContentDisposition = $"attachment; filename=\"vietqr-{record.AccountNo}.png\"";
            }

            return Results.File(pngBytes, "image/png");
        }).ExcludeFromDescription();

        return app;
    }

    internal static string ResolveRequestBaseUrl(HttpContext? ctx, string? configuredBaseUrl)
    {
        if (ctx is not null)
        {
            var forwardedHost = ctx.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var forwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();

            var host = !string.IsNullOrWhiteSpace(forwardedHost)
                ? forwardedHost
                : (ctx.Request.Host.HasValue ? ctx.Request.Host.Value : null);

            var scheme = !string.IsNullOrWhiteSpace(forwardedProto)
                ? forwardedProto
                : ctx.Request.Scheme;

            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(scheme))
            {
                return $"{scheme}://{host}".TrimEnd('/');
            }
        }

        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        return "http://localhost:5187";
    }

    internal sealed class GatewayCreateIntentPayload
    {
        [JsonPropertyName("appCode")]
        public string? AppCode { get; set; }

        [JsonPropertyName("qrContent")]
        public string? QrContent { get; set; }

        [JsonPropertyName("qrCode")]
        public string? QrCode { get; set; }
    }
}

