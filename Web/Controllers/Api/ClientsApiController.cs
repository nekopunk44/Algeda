using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Services;

namespace Web.Controllers.Api;

[Authorize(Policy = "RequireRealtorOrAdmin")]
[Route("api/clients")]
[ApiController]
public class ClientsApiController : ControllerBase
{
    private readonly AdminDashboardApiClient _adminClient;

    public ClientsApiController(AdminDashboardApiClient adminClient)
    {
        _adminClient = adminClient;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit = 500, CancellationToken cancellationToken = default)
    {
        var result = await _adminClient.GetClientsAsync(Math.Clamp(limit, 1, 500), cancellationToken);
        if (!result.IsSuccess)
            return StatusCode(500, new { error = result.Error?.Message ?? "Не удалось загрузить клиентов." });

        return Ok(result.Data);
    }
}
