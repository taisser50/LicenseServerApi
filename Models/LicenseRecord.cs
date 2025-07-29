using System; // لـ Guid, DateTime
using System.ComponentModel.DataAnnotations; // لـ [Key], [Required], [MaxLength]
using System.ComponentModel.DataAnnotations.Schema; // لـ [ForeignKey], [InverseProperty]

namespace LicenseServerApi.Models
{
    public class LicenseRecord
    {
        [Key]
        public Guid LicenseId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string HWID { get; set; } = string.Empty;

        [Required]
        public string SignedLicenseData { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastValidatedAt { get; set; }

        public bool IsActive { get; set; } = true;

       
        public DateTime ExpiryDate { get; set; }

        [ForeignKey("Voucher")]
        public string? VoucherCode { get; set; }

        [InverseProperty("LicenseRecords")]
        public Voucher? Voucher { get; set; }
    }
}