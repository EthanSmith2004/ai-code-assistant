using AiCodeAssistant.API.Auth;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiCodeAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IProjectPersistenceService _projectPersistenceService;

    public DashboardController(IProjectPersistenceService projectPersistenceService)
    {
        _projectPersistenceService = projectPersistenceService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _projectPersistenceService.GetDashboardSummaryAsync(
            User.GetRequiredUserId(),
            cancellationToken);
        return Ok(summary);
    }
}
