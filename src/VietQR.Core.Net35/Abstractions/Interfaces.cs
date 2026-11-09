using System.Collections.Generic;

namespace VietQR.Core
{
    /// <summary>
    /// Giao diện tra cứu ngân hàng theo mã BIN hoặc mã/tên viết tắt.
    /// </summary>
    public interface IBankCatalog
    {
        /// <summary>
        /// Tra cứu thông tin ngân hàng đồng bộ theo mã BIN hoặc mã/tên viết tắt (VD: 970422, MB, VCB).
        /// </summary>
        BankInfo Find(string binOrCode);

        /// <summary>
        /// Lấy toàn bộ danh sách ngân hàng thành viên NAPAS.
        /// </summary>
        IList<BankInfo> GetAll();
    }

    /// <summary>
    /// Giao diện phân giải đường dẫn file tài nguyên (logo, icon) tùy chỉnh.
    /// </summary>
    public interface IQrAssetResolver
    {
        string Resolve(string fileName);
    }

    /// <summary>
    /// Dịch vụ sinh chuỗi mã thanh toán EMVCo/NAPAS247 chạy offline 100%, không phụ thuộc I/O.
    /// </summary>
    public interface IQrGenerator
    {
        /// <summary>
        /// Sinh chuỗi mã thanh toán VietQR chuẩn EMVCo.
        /// </summary>
        string Generate(VietQrRequest request);
    }
}

