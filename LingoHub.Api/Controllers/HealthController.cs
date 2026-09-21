using LingoHub.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace LingoHub.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbOk = await _db.Database.CanConnectAsync(ct);
        var body = new
        {
            status = dbOk ? "ok" : "degraded",
            database = dbOk ? "connected" : "unreachable",
            timestamp = DateTime.UtcNow
        };
        return dbOk ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
