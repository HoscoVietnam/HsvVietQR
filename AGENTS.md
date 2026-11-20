# AGENTS.md

## Build & Verification Gotchas

- **SDK multi-targeting (`net6.0`–`net10.0`)**: `VietQR.Core` and `VietQR.Deeplink.Client` conditionally append `net9.0` when `NETCoreSdkVersion >= 9.0.100` and `net10.0` when `NETCoreSdkVersion >= 10.0.100`, allowing `dotnet restore` and `dotnet build` to succeed directly across .NET 8, 9, and 10 SDKs.
- **Conditional Deeplink Engine (`HAS_VIETQR_DEEPLINK`)**: `VietQR.Api.csproj` detects `..\VietQR.Deeplink\VietQR.Deeplink.csproj` at compile time, defines `HAS_VIETQR_DEEPLINK`, and wires `DeeplinkHostExtensions.AddDeeplinkServer` + `UseDeeplinkServer`. When present, `LocalVietQrDeeplinkClient` overrides `IVietQrDeeplinkClient` so `/api/v1/vietqr-core/deeplink/*` executes in-process with zero network I/O.
- **WinForms .NET 3.5 Offline Build (`VietQR.Core.Net35`)**: Targets `net35` using local reference assemblies (`C:\Windows\Microsoft.NET\Framework\v2.0.50727` and `v3.5`) with `<AutomaticallyUseReferenceAssemblyPackages>false</AutomaticallyUseReferenceAssemblyPackages>` and `<_EnableDefaultWindowsPlatform>false</_EnableDefaultWindowsPlatform>` so MSBuild compiles without registry or NuGet access. Keep public APIs C# 3.0 / VS 2008 caller-friendly by providing explicit method overloads alongside default parameters and a parameterless constructor on services.

## Domain Invariants (NAPAS247 / EMVCo)

- **TLV byte-length measurement**: Measure EMVCo Tag-Length-Value lengths in UTF-8 byte octets (`Encoding.UTF8.GetByteCount`), never UTF-16 `.Length`.
- **Service type separation**: `VietQrServiceType.Account` maps to `QRIBFTTA` (renders label `"Số TK"`), while `VietQrServiceType.Card` maps to `QRIBFTTC` (renders label `"Số thẻ"`). Require explicit `serviceType` selection rather than guessing from digit count.
- **Flexible bank identifiers**: Accept 6-digit NAPAS BINs (`"970422"`), bank `Code` (`"MB"`), `ShortName` (`"MBBank"`), or aliases across `BankBin.Create`, `VietQr.SetBeneficiaryOrganization`, `VietQrGenerator`, and `DeeplinkAppCatalog.Find`.
- **Static asset bridge**: `VietQR.Deeplink` copies `wwwroot/icons/*.png` into `wwwroot/_content/VietQR.Deeplink/icons/` on build/publish; `VietQR.Api` mounts that directory at `/icons`. When adding a new app to `DeeplinkAppCatalog`, add its lowercase `{appCode}.png` icon to `src/VietQR.Deeplink/wwwroot/icons/`.

## Context Pointers

- **Core EMVCo payload generation, SkiaSharp rendering (`compact`/`print`/`qr_only`), SVG output, or ZXing decoding** → read [`src/VietQR.Core/README.md`](src/VietQR.Core/README.md).
- **Legacy .NET Framework 3.5 WinForms integration, `System.Drawing` GDI+ rendering, or `api.vietqr.io/v2/generate` offline drop-in replacement (`VietQrGenerator`)** → read [`src/VietQR.Core.Net35/README.md`](src/VietQR.Core.Net35/README.md).
- **Deeplink server engine, 44+ banking app schemes/packages (`SupportsAutofill` vs app launch), `/p/{token}` Smart Payment Bridge, or `/v1/intents` gateway endpoints** → read [`src/VietQR.Deeplink/README.md`](src/VietQR.Deeplink/README.md).
- **Deeplink HTTP Client SDK, endpoint discovery (`vietqr-discovery.json`), or external ASP.NET Core DI registration (`AddVietQrDeeplink`)** → read [`src/VietQR.Deeplink.Client/README.md`](src/VietQR.Deeplink.Client/README.md).
- **Coding conventions, Vietnamese commit message format, or PR requirements** → read [`CONTRIBUTING.md`](CONTRIBUTING.md).
