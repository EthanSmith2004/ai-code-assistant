using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Projects;
using Microsoft.AspNetCore.Mvc;

namespace AiCodeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectPersistenceService _projectPersistenceService;

    public ProjectsController(IProjectPersistenceService projectPersistenceService)
    {
        _projectPersistenceService = projectPersistenceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await _projectPersistenceService.GetProjectsAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> SaveProject(
        SaveProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectPersistenceService.SaveProjectAsync(request, cancellationToken);
            return Ok(project);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("{projectId:guid}/analyses")]
    public async Task<ActionResult<IReadOnlyList<ProjectAnalysisDto>>> GetProjectAnalyses(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var analyses = await _projectPersistenceService.GetProjectAnalysesAsync(projectId, cancellationToken);
        return Ok(analyses);
    }

    [HttpPost("{projectId:guid}/analyses")]
    public async Task<ActionResult<ProjectAnalysisDto>> SaveAnalysis(
        Guid projectId,
        SaveAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await _projectPersistenceService.SaveAnalysisAsync(projectId, request, cancellationToken);
            return Ok(analysis);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
