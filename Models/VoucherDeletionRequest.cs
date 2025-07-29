using System.ComponentModel.DataAnnotations;

namespace LicenseServerApi.Models
{
    public class VoucherDeletionRequest
    {
        [Required]
        [MaxLength(50)]
        public string VoucherCode { get; set; } = string.Empty;
    }
}