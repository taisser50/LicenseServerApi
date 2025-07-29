using System; // لـ DateTime
using System.Collections.Generic; // لـ ICollection
using System.ComponentModel.DataAnnotations; // لـ [Key], [MaxLength]
using System.ComponentModel.DataAnnotations.Schema; // لـ [InverseProperty]

namespace LicenseServerApi.Models
{
    public class Voucher
    {
        [Key]
        [MaxLength(50)]
        public string VoucherCode { get; set; } = string.Empty;

        public int DurationDays { get; set; }
        public int AllowedDevices { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Description { get; set; }

        public int UsageCount { get; set; } = 0; // القيمة الافتراضية
        public bool IsActive { get; set; } = true; // القيمة الافتراضية
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow; // القيمة الافتراضية

        [InverseProperty("Voucher")]
        public ICollection<LicenseRecord> LicenseRecords { get; set; } = new List<LicenseRecord>();
    }
}