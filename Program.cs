using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore; // <--- هذا لاستخدام Entity Framework Core (UseSqlServer)
using LicenseServerApi.Data; // <--- هذا لـ AppDbContext الخاص بنا
using Microsoft.Extensions.Configuration; // <--- هذا لقراءة المفتاح السري من appsettings.json
// لا نحتاج لـ usings لـ Helpers و Models هنا مباشرة
// لأنهم يتم استخدامهما داخل Controllers وليس في هذا الملف بشكل مباشر،
// لكن إذا أضفت كود يستخدمهم مباشرة هنا، ستحتاج لاستيرادهم.

var builder = WebApplication.CreateBuilder(args);

// أضف خدماتك هنا (Dependency Injection)


// 1. تسجيل DbContext الخاص بنا
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. إضافة Controllers (لوحات التحكم للـ API)
builder.Services.AddControllers();

// 3. تكوين Swagger/OpenAPI لتوثيق الـ API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// تهيئة طلبات HTTP (HTTP Request Pipeline)

// 1. استخدام Swagger UI في وضع التطوير فقط
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // تمكين Swagger Middleware
    app.UseSwaggerUI();  // تمكين Swagger UI
}

// 2. إعادة توجيه HTTPS (يمكن تعليق هذا مؤقتًا للاختبار على HTTP)
// إذا كنت تستخدم HTTPS، تأكد من تثبيت الشهادة: dotnet dev-certs https --trust
// app.UseHttpsRedirection();

// 3. تمكين نظام الصلاحيات (Authorization)
app.UseAuthorization();

// 4. ربط الـ Controllers بالمسارات
app.MapControllers();

// 5. نقطة نهاية اختبار بسيطة (يمكنك إزالتها بعد التأكد من عمل الـ API)
app.MapGet("/test", () => "Hello from License API Test!");

app.Run(); // تشغيل التطبيق