namespace VietQR.Core;

public sealed record BankBin
{
    public string Value { get; }

    private BankBin(string value) => Value = value;

    public static BankBin Create(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Mã BIN hoặc mã ngân hàng không được để trống.", nameof(raw));

        if (value.Length == 6 && value.All(c => c >= '0' && c <= '9'))
            return new BankBin(value);

        var bank = EmbeddedBankCatalog.Instance.Find(value);
        if (bank is not null && !string.IsNullOrEmpty(bank.Bin) && bank.Bin.Length == 6 && bank.Bin.All(c => c >= '0' && c <= '9'))
            return new BankBin(bank.Bin);

        throw new ArgumentException("BIN phải gồm đúng 6 chữ số hoặc là mã/tên viết tắt ngân hàng hợp lệ (VD: 970422, MB, VCB).", nameof(raw));
    }

    public override string ToString() => Value;

    public static implicit operator string(BankBin bin) => bin.Value;
    public static implicit operator BankBin(string raw) => Create(raw);
}
