using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LingoHub.Api;

// ============================================================
// 文件作用：给“管理员才能用”的接口加一把锁。
//
// 为什么需要：网站部署到公网后，任何人都能调用上传接口，
//   上传 PDF 会调用 Voyage API（花你的钱），还会往数据库里塞垃圾数据。
//
// 用法：在 Controller 的方法上加 [RequireAdminKey]。
//   调用时要带请求头：X-Admin-Key: <密钥>
//   密钥从配置 Admin:ApiKey 读取（部署时写在 .env 的 ADMIN_API_KEY 里）
//
// 没有配置密钥时：
//   本地开发（Development）→ 放行，方便你自己测试
//   其它环境（部署后）      → 一律拒绝，保证“忘了配置”也是安全的
// ============================================================
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var expected = services.GetRequiredService<IConfiguration>()["Admin:ApiKey"];

        if (string.IsNullOrEmpty(expected))
        {
            if (services.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                return;
            context.Result = new ObjectResult(new { error = "This endpoint is disabled: Admin:ApiKey is not configured." })
                { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        // FixedTimeEquals：比较时间不随“猜对了几个字符”变化，防止别人一个字符一个字符地猜密钥
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected)))
            context.Result = new UnauthorizedObjectResult(new { error = $"Missing or wrong {HeaderName} header." });
    }
}
