using System;
using System.ComponentModel.DataAnnotations; 

namespace LicenseServerApi.Models 
{
    public class LicenseInfo
    {
        [Required] 
        public required string ClientName { get; set; }
        [Required] 
        public required string HWID { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}