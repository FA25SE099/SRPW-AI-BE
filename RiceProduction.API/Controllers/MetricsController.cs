using Microsoft.AspNetCore.Mvc;

namespace RiceProduction.API.Controllers;

[ApiController]
public class MetricsController : ControllerBase
{
    [HttpGet("/metrics")]
    public IActionResult GetMetrics()
    {
        var metrics = $@"
# HELP riceproduction_up Application status
# TYPE riceproduction_up gauge
riceproduction_up 1

# HELP riceproduction_info Application info
# TYPE riceproduction_info gauge
riceproduction_info{{version=""1.0.0"",environment=""production""}} 1

# HELP riceproduction_timestamp Current timestamp
# TYPE riceproduction_timestamp gauge
riceproduction_timestamp {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}
";
        return Content(metrics, "text/plain; version=0.0.4");
    }

    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            uptime = Environment.TickCount64 / 1000
        });
    }
}