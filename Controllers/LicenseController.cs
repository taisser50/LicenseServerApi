using Microsoft.AspNetCore.Mvc;
using LicenseServerApi.Data;
using LicenseServerApi.Models;
using LicenseServerApi.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Linq; 

namespace LicenseServerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LicenseController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _signingKey;
        private readonly ILogger<LicenseController> _logger;
        private readonly int _offlineGracePeriodDays; 

        public LicenseController(AppDbContext context, IConfiguration configuration, ILogger<LicenseController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

         
            _signingKey = configuration["LicenseSettings:SigningKey"] ?? throw new InvalidOperationException("License signing key is not configured.");

          
            _offlineGracePeriodDays = configuration.GetValue<int>("LicenseSettings:OfflineGracePeriodDays", 2);
            if (_offlineGracePeriodDays < 0)
            {
                _logger.LogWarning("OfflineGracePeriodDays in configuration is negative. Defaulting to 2 days.");
                _offlineGracePeriodDays = 2; 
            }

            
            if (_signingKey.Length < 16)
            {
                _logger.LogWarning("License signing key is too short. It should be at least 16 characters for stronger security.");
            }
        }

        // ---------- نقطة نهاية لتسجيل ترخيص جديد ----------
        [HttpPost("register")]
        public async Task<IActionResult> RegisterLicense([FromBody] LicenseRegistrationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // التحقق من وجود ترخيص حالي لنفس HWID ونشط
            var existingLicense = await _context.Licenses
                .FirstOrDefaultAsync(l => l.HWID == request.HWID && l.IsActive);

            if (existingLicense != null)
            {
                _logger.LogInformation($"Attempted to register existing HWID: {request.HWID}. License ID: {existingLicense.LicenseId}");
                return Conflict($"A valid license already exists for HWID: {request.HWID}. License ID: {existingLicense.LicenseId}");
            }

          
            int durationDays;
            string? voucherCodeUsed = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
             
                var voucher = await _context.Vouchers.SingleOrDefaultAsync(v => v.VoucherCode == request.VoucherCode);

                if (voucher == null)
                {
                    _logger.LogWarning($"Invalid voucher code provided: {request.VoucherCode}");
                    return BadRequest("Invalid voucher code.");
                }
                if (!voucher.IsActive)
                {
                    _logger.LogWarning($"Attempted to use inactive voucher: {request.VoucherCode}");
                    return BadRequest("Voucher is inactive or expired.");
                }
                if (voucher.UsageCount >= voucher.AllowedDevices)
                {
                    _logger.LogWarning($"Voucher {request.VoucherCode} has reached its maximum usage limit ({voucher.UsageCount}/{voucher.AllowedDevices}).");
                    return BadRequest("Voucher has reached its maximum usage limit.");
                }
                if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning($"Attempted to use expired voucher: {request.VoucherCode}");
                    return BadRequest("Voucher has expired.");
                }

                durationDays = voucher.DurationDays;
                voucherCodeUsed = voucher.VoucherCode;

             
                voucher.UsageCount++;
                _context.Vouchers.Update(voucher);
                _logger.LogInformation($"Voucher {voucher.VoucherCode} usage count incremented to {voucher.UsageCount}.");
            }
            else if (request.Days.HasValue && request.Days.Value > 0)
            {
               
                durationDays = request.Days.Value;
            }
            else
            {
                _logger.LogWarning("License registration request missing VoucherCode or Days.");
                return BadRequest("Either a valid VoucherCode or a positive number of Days must be provided.");
            }

            DateTime expiryDate = DateTime.UtcNow.AddDays(durationDays);

            try
            {
               
                string signedLicenseData = CryptoHelper.EncryptLicense(request.ClientName, request.HWID, expiryDate, _signingKey);

             
                var licenseRecord = new LicenseRecord
                {
                    LicenseId = Guid.NewGuid(), 
                    ClientName = request.ClientName,
                    HWID = request.HWID,
                    ExpiryDate = expiryDate,
                    SignedLicenseData = signedLicenseData,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastValidatedAt = DateTime.UtcNow, 
                    VoucherCode = voucherCodeUsed 
                };

                _context.Licenses.Add(licenseRecord);
                await _context.SaveChangesAsync(); 

                _logger.LogInformation($"License {licenseRecord.LicenseId} registered for HWID: {request.HWID} using voucher: {voucherCodeUsed ?? "N/A"}. Expiry: {expiryDate}");

                return Ok(new
                {
                    LicenseId = licenseRecord.LicenseId,
                    Message = "License registered successfully.",
                    ExpiryDate = expiryDate,
                    VoucherCodeUsed = voucherCodeUsed 
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DbUpdateException occurred during license registration or voucher update for HWID: {HWID}", request.HWID);
                return StatusCode(500, $"An error occurred during registration: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during license registration for HWID: {HWID}", request.HWID);
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }


        // ---------- نقطة نهاية للتحقق من ترخيص موجود ----------
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateLicense([FromBody] LicenseValidationRequest request)
        {
            // التحقق من صحة المدخلات
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Guid licenseGuid;
            if (!Guid.TryParse(request.LicenseId, out licenseGuid))
            {
                _logger.LogWarning($"Invalid License ID format received: {request.LicenseId}");
                return BadRequest("Invalid License ID format.");
            }

          
           
            var licenseRecord = await _context.Licenses
                .Include(l => l.Voucher) 
                .FirstOrDefaultAsync(l => l.LicenseId == licenseGuid);

            if (licenseRecord == null)
            {
                _logger.LogWarning($"License ID {request.LicenseId} not found for validation.");
                return Unauthorized("License not found.");
            }

           
            if (!licenseRecord.IsActive)
            {
                _logger.LogWarning($"License ID {request.LicenseId} is inactive (e.g., revoked or expired).");
                return Unauthorized("License is inactive (e.g., revoked or expired).");
            }

            // التحقق من HWID
            if (licenseRecord.HWID != request.HWID)
            {
                _logger.LogWarning($"HWID mismatch for License ID {request.LicenseId}. Expected: {licenseRecord.HWID}, Received: {request.HWID}.");
                return Unauthorized("License is not valid for this device (HWID mismatch).");
            }

          
            if (licenseRecord.Voucher != null) 
            {
                if (!licenseRecord.Voucher.IsActive) 
                {
                    _logger.LogWarning($"License ID {request.LicenseId} associated with deactivated voucher {licenseRecord.VoucherCode}.");
                  
                    licenseRecord.IsActive = false;
                    await _context.SaveChangesAsync();
                    return Unauthorized("Associated voucher has been deactivated.");
                }
            }

            try
            {
                // فك تشفير والتحقق من التوقيع باستخدام CryptoHelper والمفتاح السري للسيرفر
                LicenseInfo decryptedInfo = CryptoHelper.DecryptLicense(licenseRecord.SignedLicenseData, _signingKey);

                // التحقق من تاريخ انتهاء الصلاحية (من البيانات المشفرة)
                if (decryptedInfo.ExpiryDate < DateTime.UtcNow)
                {
                    _logger.LogInformation($"License ID {request.LicenseId} expired on {decryptedInfo.ExpiryDate}. Deactivating.");
                    // تحديث حالة الترخيص في قاعدة البيانات إلى غير نشط
                    licenseRecord.IsActive = false;
                    await _context.SaveChangesAsync();
                    return Unauthorized("License expired.");
                }

                licenseRecord.LastValidatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"License ID {request.LicenseId} validated successfully. Client: {decryptedInfo.ClientName}");

                // 💡 إرجاع OfflineGracePeriodDays مع الاستجابة
                return Ok(new
                {
                    IsValid = true,
                    ExpiryDate = decryptedInfo.ExpiryDate,
                    ClientName = decryptedInfo.ClientName,
                    OfflineGracePeriodDays = _offlineGracePeriodDays // 💡 الخاصية الجديدة المضافة
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during license validation for ID {LicenseId}. Data integrity issue or invalid signature.", request.LicenseId);
                return StatusCode(500, $"License validation failed due to an internal error: {ex.Message}");
            }
        }

        // ---------- نقطة نهاية لإنشاء القسائم (Admin Function) ----------
        [HttpPost("GenerateVoucher")]
        // يمكنك إضافة [Authorize(Roles = "Admin")] هنا لتقييد الوصول للمسؤولين فقط
        public async Task<IActionResult> GenerateVoucher([FromBody] VoucherGenerationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

          
            string newVoucherCode = GenerateUniqueVoucherCode();

            var voucher = new Voucher
            {
                VoucherCode = newVoucherCode,
                AllowedDevices = request.AllowedDevices,
                DurationDays = request.DurationDays,
                Description = request.Description,
                ExpiryDate = request.ExpiryDate
            };

            try
            {
                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Voucher {voucher.VoucherCode} generated successfully. Allowed Devices: {voucher.AllowedDevices}, Duration: {voucher.DurationDays} days.");
                return Ok(new { Message = "Voucher generated successfully.", VoucherCode = newVoucherCode });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DbUpdateException occurred during voucher generation. Likely a duplicate voucher code: {VoucherCode}", newVoucherCode);
                if (dbEx.InnerException != null && dbEx.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict($"Failed to generate voucher. A voucher with code '{newVoucherCode}' might already exist. Try again.");
                }
                return StatusCode(500, $"An error occurred during voucher generation: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during voucher generation.");
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

        // ---------- نقطة نهاية للحصول على تفاصيل قسيمة معينة (للمسؤولين) ----------
        [HttpGet("Voucher/{voucherCode}")]
        // يمكنك إضافة [Authorize(Roles = "Admin")] هنا
        public async Task<IActionResult> GetVoucherDetails(string voucherCode)
        {
            var voucher = await _context.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.VoucherCode == voucherCode);
            if (voucher == null)
            {
                return NotFound("Voucher not found.");
            }
            return Ok(voucher);
        }

        // ---------- دالة مساعدة لتوليد كود قسيمة فريد ----------
        private string GenerateUniqueVoucherCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpPost("deactivate-voucher")]
        public async Task<IActionResult> DeactivateVoucher([FromBody] VoucherDeletionRequest request)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for deactivate-voucher request.");
                return BadRequest(ModelState);
            }

            try
            {
                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherCode == request.VoucherCode);

                if (voucher == null)
                {
                    _logger.LogWarning($"Attempted to deactivate non-existent voucher: {request.VoucherCode}");
                    return NotFound("Voucher not found.");
                }

                if (!voucher.IsActive)
                {
                    _logger.LogInformation($"Voucher {request.VoucherCode} is already inactive. No action taken.");
                    return Ok(new { Message = "Voucher is already inactive." });
                }

                voucher.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Voucher {request.VoucherCode} deactivated successfully.");
                return Ok(new { Message = $"Voucher {request.VoucherCode} deactivated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating voucher {VoucherCode}.", request.VoucherCode);
                return StatusCode(500, $"An error occurred while deactivating the voucher: {ex.Message}");
            }
        }
        // ---------- نماذج طلبات واستجابات API (كلاسات داخلية) ----------

        public class LicenseRegistrationRequest
        {
            [Required(ErrorMessage = "Client name is required.")]
            [StringLength(255)]
            public string ClientName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Hardware ID is required.")]
            [StringLength(255)]
            public string HWID { get; set; } = string.Empty;

            public string? VoucherCode { get; set; }

            [Range(1, 3650, ErrorMessage = "Days must be between 1 and 3650.")]
            public int? Days { get; set; }
        }

        public class LicenseValidationRequest
        {
            [Required(ErrorMessage = "License ID is required.")]
            public string LicenseId { get; set; } = string.Empty;

            [Required(ErrorMessage = "Hardware ID is required.")]
            [StringLength(255)]
            public string HWID { get; set; } = string.Empty;
        }

        public class VoucherGenerationRequest
        {
            [Required(ErrorMessage = "Number of allowed devices is required.")]
            [Range(1, int.MaxValue, ErrorMessage = "Allowed devices must be at least 1.")]
            public int AllowedDevices { get; set; }

            [Required(ErrorMessage = "Duration in days is required.")]
            [Range(1, int.MaxValue, ErrorMessage = "Duration must be at least 1 day.")]
            public int DurationDays { get; set; }

            [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
            public string? Description { get; set; }

            public DateTime? ExpiryDate { get; set; }
        }
    }
}