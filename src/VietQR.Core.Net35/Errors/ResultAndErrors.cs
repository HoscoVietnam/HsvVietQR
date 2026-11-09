using System;

namespace VietQR.Core
{
    /// <summary>
    /// Biểu diễn thông tin lỗi nghiệp vụ hoặc lỗi phân tích cú pháp VietQR.
    /// </summary>
    public struct Error : IEquatable<Error>
    {
        public static readonly Error None = new Error(string.Empty, string.Empty);

        public string Code { get; private set; }
        public string Message { get; private set; }

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Code); }
        }

        public Error(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Equals(Error other)
        {
            return string.Equals(Code, other.Code, StringComparison.Ordinal) &&
                   string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Error && Equals((Error)obj);
        }

        public override int GetHashCode()
        {
            return ((Code != null ? Code.GetHashCode() : 0) * 397) ^
                   (Message != null ? Message.GetHashCode() : 0);
        }

        public override string ToString()
        {
            if (IsEmpty) return string.Empty;
            return string.Format("[{0}] {1}", Code, Message);
        }
    }

    /// <summary>
    /// Kết quả thực thi trả về giá trị hoặc lỗi (không ném Exception).
    /// </summary>
    public sealed class Result<T>
    {
        public bool IsSuccess { get; private set; }
        public T Value { get; private set; }
        public Error Error { get; private set; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = Error.None;
        }

        private Result(Error error)
        {
            IsSuccess = false;
            Value = default(T);
            Error = error;
        }

        public static Result<T> Ok(T value)
        {
            return new Result<T>(value);
        }

        public static Result<T> Fail(Error error)
        {
            return new Result<T>(error);
        }

        public static implicit operator Result<T>(T value)
        {
            return Ok(value);
        }

        public static implicit operator Result<T>(Error error)
        {
            return Fail(error);
        }
    }

    /// <summary>
    /// Danh mục các mã lỗi chuẩn của VietQR.Core.
    /// </summary>
    public static class VietQrErrors
    {
        public static readonly Error MissingParam = new Error("qr.missing_param", "Thiếu tham số bắt buộc (bankBin, accountNo, amount, description).");
        public static readonly Error InvalidAmount = new Error("qr.invalid_amount", "Số tiền không hợp lệ (nhỏ hơn hoặc bằng 0).");
        public static readonly Error InvalidBin = new Error("qr.invalid_bin", "Mã BIN sai định dạng (bắt buộc đúng 6 chữ số).");
        public static readonly Error BankNotFound = new Error("qr.bank_not_found", "Không tìm thấy thông tin ngân hàng trong danh mục NAPAS.");
        public static readonly Error InvalidTemplate = new Error("qr.invalid_template", "Template không hợp lệ (chỉ chấp nhận 'compact', 'qr_only' hoặc 'print').");
        public static readonly Error SystemError = new Error("qr.system_error", "Lỗi xử lý hệ thống.");

        public static readonly Error InvalidPayload = new Error("qr.invalid_payload", "Chuỗi mã QR không đúng định dạng EMVCo TLV hoặc vượt quá độ dài cho phép.");
        public static readonly Error InvalidCrc = new Error("qr.invalid_crc", "Mã kiểm tra CRC16 (Tag 63) không khớp với nội dung dữ liệu mã QR.");
        public static readonly Error NotVietQr = new Error("qr.not_vietqr", "Mã QR không phải là mã thanh toán VietQR hợp lệ (thiếu Tag 38 hoặc AID A000000727).");
        public static readonly Error ImageDecodeFailed = new Error("qr.image_decode_failed", "Không tìm thấy hoặc không thể giải mã mã QR từ hình ảnh cung cấp.");
    }
}

