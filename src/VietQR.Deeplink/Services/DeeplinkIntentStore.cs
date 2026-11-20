using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VietQR.Core;

namespace VietQR.Deeplink;

/// <summary>
/// Quản lý lưu trữ, tra cứu và tự động dọn dẹp các phiên điều hướng thanh toán ngắn hạn (<see cref="DeeplinkIntentRecord"/>),
/// kèm cơ chế Stateless Signed Token (HMAC-SHA256) dự phòng.
/// </summary>
public sealed class DeeplinkIntentStore
{
    private static readonly char[] Base62Chars =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

    private readonly ConcurrentDictionary<string, DeeplinkIntentRecord> store = new(StringComparer.Ordinal);
    private readonly DeeplinkAppCatalog catalog;
    private readonly IBankCatalog bankCatalog;
    private readonly DeeplinkServerOptions options;
    private readonly byte[] hmacKey;
    private long lastCleanupTicks = DateTime.UtcNow.Ticks;

    public DeeplinkIntentStore(
        DeeplinkAppCatalog catalog,
        IBankCatalog bankCatalog,
        IOptions<DeeplinkServerOptions> options)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.bankCatalog = bankCatalog ?? throw new ArgumentNullException(nameof(bankCatalog));
        this.options = options?.Value ?? new DeeplinkServerOptions();
        this.hmacKey = !string.IsNullOrWhiteSpace(this.options.TokenSecret)
            ? SHA256.HashData(Encoding.UTF8.GetBytes(this.options.TokenSecret.Trim()))
            : RandomNumberGenerator.GetBytes(32);
    }

    /// <summary>
    /// Khởi tạo phiên điều hướng mới từ mã ứng dụng (<paramref name="appCode"/>) và chuỗi EMVCo (<paramref name="qrContent"/>).
    /// </summary>
    public DeeplinkIntentRecord? CreateIntent(string appCode, string qrContent)
    {
        var record = BuildIntentRecord(
            token: null,
            appCode: appCode,
            qrContent: qrContent,
            createdAtUtc: DateTime.UtcNow,
            expiresAtUtc: DateTime.UtcNow.AddSeconds(Math.Clamp(options.IntentTtlSeconds, 30, 3600)));

        if (record is null)
        {
            return null;
        }

        CleanupExpiredIfNeeded();
        store[record.Token] = record;
        return record;
    }

    /// <summary>
    /// Lấy thông tin phiên điều hướng theo mã <paramref name="token"/> (chỉ trả về khi chưa hết hạn).
    /// Hỗ trợ cả Short Token trong RAM lẫn Stateless Signed Token (<c>s.{payload}.{sig}</c>).
    /// </summary>
    public DeeplinkIntentRecord? GetActiveIntent(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var trimmed = token.Trim();
        if (store.TryGetValue(trimmed, out var record))
        {
            if (DateTime.UtcNow <= record.ExpiresAtUtc)
            {
                return record;
            }

            store.TryRemove(trimmed, out _);
            return null;
        }

        if (trimmed.StartsWith("s.", StringComparison.Ordinal))
        {
            return TryDecodeStatelessToken(trimmed);
        }

        return null;
    }

    /// <summary>
    /// Phân giải phiên điều hướng từ <paramref name="token"/> (bao gồm Short Token, Stateless Token, hoặc Demo URL <c>demo-*</c>).
    /// </summary>
    public DeeplinkIntentRecord? ResolveIntent(string token, IQueryCollection? query = null)
    {
        var record = GetActiveIntent(token);
        if (record is not null)
        {
            return record;
        }

        if (!string.IsNullOrWhiteSpace(token) && token.StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateDemoIntent(
                token,
                query?["ba"].FirstOrDefault(),
                query?["am"].FirstOrDefault(),
                query?["tn"].FirstOrDefault(),
                query?["bin"].FirstOrDefault());
        }

        return null;
    }

    /// <summary>
    /// Tạo mã Stateless Token được ký bằng HMAC-SHA256 (<c>s.{base64url}.{hmac}</c>) không phụ thuộc bộ nhớ RAM.
    /// </summary>
    public string CreateStatelessToken(DeeplinkIntentRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var expUnix = new DateTimeOffset(record.ExpiresAtUtc).ToUnixTimeSeconds();
        var rawPayload = $"{record.TargetApp.AppCode}|{expUnix.ToString(CultureInfo.InvariantCulture)}|{record.QrContent}";
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(rawPayload));
        var sigBytes = HMACSHA256.HashData(hmacKey, Encoding.UTF8.GetBytes(payloadB64));
        var sigB64 = Base64UrlEncode(sigBytes.AsSpan(0, 16));
        return $"s.{payloadB64}.{sigB64}";
    }

    private DeeplinkIntentRecord? TryDecodeStatelessToken(string statelessToken)
    {
        var parts = statelessToken.Split('.', 3);
        if (parts.Length != 3)
        {
            return null;
        }

        var payloadB64 = parts[1];
        var providedSig = parts[2];

        var expectedSigBytes = HMACSHA256.HashData(hmacKey, Encoding.UTF8.GetBytes(payloadB64));
        var expectedSig = Base64UrlEncode(expectedSigBytes.AsSpan(0, 16));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(providedSig),
                Encoding.ASCII.GetBytes(expectedSig)))
        {
            return null;
        }

        try
        {
            var rawPayload = Encoding.UTF8.GetString(Base64UrlDecode(payloadB64));
            var fields = rawPayload.Split('|', 3);
            if (fields.Length != 3 ||
                !long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expUnix))
            {
                return null;
            }

            var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            if (DateTime.UtcNow > expiresAtUtc)
            {
                return null;
            }

            return BuildIntentRecord(
                token: statelessToken,
                appCode: fields[0],
                qrContent: fields[2],
                createdAtUtc: DateTime.UtcNow,
                expiresAtUtc: expiresAtUtc);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tạo bản ghi Intent tạm thời cho chế độ Demo trực tiếp trên URL (<c>/p/demo-{appCode}?ba=...&amp;am=...</c>).
    /// </summary>
    public DeeplinkIntentRecord? TryCreateDemoIntent(
        string token,
        string? bankAccountQuery,
        string? amountQuery,
        string? descriptionQuery,
        string? bankBinQuery)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            !token.StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var appCode = token["demo-".Length..];
        var targetApp = catalog.Find(appCode) ?? catalog.Find("MB")!;

        var accountNo = string.IsNullOrWhiteSpace(bankAccountQuery) ? "056789799999" : bankAccountQuery.Trim();
        var receiverBin = !string.IsNullOrWhiteSpace(bankBinQuery)
            ? bankBinQuery.Trim()
            : (targetApp.Bin ?? "970422");

        if (accountNo.Contains('@'))
        {
            var parts = accountNo.Split('@', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                accountNo = parts[0];
                var bankFromSuffix = bankCatalog.Find(parts[1]);
                if (bankFromSuffix is not null)
                {
                    receiverBin = bankFromSuffix.Bin;
                }
            }
        }

        long? parsedAmount = null;
        if (!string.IsNullOrWhiteSpace(amountQuery) &&
            long.TryParse(amountQuery.Replace(",", "").Replace(".", "").Trim(), out var amt) &&
            amt > 0)
        {
            parsedAmount = amt;
        }

        var desc = string.IsNullOrWhiteSpace(descriptionQuery) ? "thanh toan don hang" : descriptionQuery.Trim();
        var qrPayload = VietQrPayload.Create(
            BankBin.Create(receiverBin),
            accountNo,
            parsedAmount,
            desc);

        var bankInfo = bankCatalog.Find(receiverBin);
        var now = DateTime.UtcNow;

        return new DeeplinkIntentRecord
        {
            Token = token,
            TargetApp = targetApp,
            QrContent = qrPayload,
            ReceiverBankBin = receiverBin,
            ReceiverBankCode = ResolveReceiverBankCode(receiverBin, bankInfo),
            ReceiverBankShortName = bankInfo?.ShortName ?? bankInfo?.Code ?? receiverBin,
            AccountNo = accountNo,
            AccountName = "VO NGOC HIEN",
            Amount = parsedAmount,
            Description = desc,
            ServiceType = VietQrServiceType.Account,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(Math.Clamp(options.IntentTtlSeconds, 30, 3600))
        };
    }

    private DeeplinkIntentRecord? BuildIntentRecord(
        string? token,
        string appCode,
        string qrContent,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(appCode) || string.IsNullOrWhiteSpace(qrContent))
        {
            return null;
        }

        var targetApp = catalog.Find(appCode);
        if (targetApp is null)
        {
            return null;
        }

        var trimmedQr = qrContent.Trim();
        var parseResult = VietQrParser.Parse(trimmedQr, new VietQrParseOptions { BankCatalog = bankCatalog });
        if (!parseResult.IsSuccess || parseResult.Value is null || !parseResult.Value.IsValidCrc)
        {
            return null;
        }

        var info = parseResult.Value;
        var receiverBankInfo = info.BankInfo ?? bankCatalog.Find(info.BankBin);
        var receiverBankCode = ResolveReceiverBankCode(info.BankBin, receiverBankInfo);
        var receiverShortName = receiverBankInfo?.ShortName ?? receiverBankInfo?.Code ?? info.BankBin;

        string? accountName = null;
        if (info.RawTags.TryGetValue("59", out var rawName) && !string.IsNullOrWhiteSpace(rawName))
        {
            accountName = rawName.Trim();
        }

        return new DeeplinkIntentRecord
        {
            Token = string.IsNullOrWhiteSpace(token) ? GenerateUniqueToken() : token,
            TargetApp = targetApp,
            QrContent = trimmedQr,
            ReceiverBankBin = info.BankBin,
            ReceiverBankCode = receiverBankCode,
            ReceiverBankShortName = receiverShortName,
            AccountNo = info.AccountNo,
            AccountName = accountName,
            Amount = info.Amount,
            Description = info.Description,
            ServiceType = info.ServiceType,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private string ResolveReceiverBankCode(string bankBin, BankInfo? bankInfo)
    {
        var matchedApp = catalog.Find(bankBin);
        if (matchedApp is not null)
        {
            return matchedApp.AutofillAppId.ToLowerInvariant();
        }

        if (bankInfo is not null && !string.IsNullOrWhiteSpace(bankInfo.Code))
        {
            return bankInfo.Code.Trim().ToLowerInvariant();
        }

        return bankBin;
    }

    private string GenerateUniqueToken()
    {
        Span<byte> randomBytes = stackalloc byte[8];
        Span<char> chars = stackalloc char[8];

        for (int attempt = 0; attempt < 5; attempt++)
        {
            RandomNumberGenerator.Fill(randomBytes);
            for (int i = 0; i < 8; i++)
            {
                chars[i] = Base62Chars[randomBytes[i] % Base62Chars.Length];
            }

            var candidate = new string(chars);
            if (!store.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N")[..10];
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private void CleanupExpiredIfNeeded()
    {
        var now = DateTime.UtcNow;
        var prevTicks = Interlocked.Read(ref lastCleanupTicks);

        if ((now - new DateTime(prevTicks, DateTimeKind.Utc)).TotalSeconds < 30 &&
            store.Count < options.MaxActiveIntents)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref lastCleanupTicks, now.Ticks, prevTicks) != prevTicks)
        {
            return;
        }

        foreach (var kvp in store)
        {
            if (now > kvp.Value.ExpiresAtUtc)
            {
                store.TryRemove(kvp.Key, out _);
            }
        }
    }
}
