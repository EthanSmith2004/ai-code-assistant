using AiCodeAssistant.API.Samples;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Contracts.Projects;
using AiCodeAssistant.Domain.Graph;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiCodeAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly ICodebaseAnalysisService _codebaseAnalysisService;
    private readonly ICodeGraphSimplifier _codeGraphSimplifier;

    public AnalysisController(
        ICodebaseAnalysisService codebaseAnalysisService,
        ICodeGraphSimplifier codeGraphSimplifier)
    {
        _codebaseAnalysisService = codebaseAnalysisService;
        _codeGraphSimplifier = codeGraphSimplifier;
    }

    [HttpGet("samples")]
    public ActionResult<IReadOnlyList<SampleProjectDto>> Samples([FromServices] SampleCatalog sampleCatalog)
    {
        return Ok(sampleCatalog.GetAll());
    }

    [HttpPost("scan")]
    public async Task<ActionResult<CodeGraph>> Scan(ProjectScanRequest request, CancellationToken cancellationToken)
    {
        var graph = await _codebaseAnalysisService.AnalyzeAsync(request, cancellationToken);
        return Ok(graph);
    }

    [HttpPost("scan/simplified")]
    public async Task<ActionResult<CodeGraph>> ScanSimplified(ProjectScanRequest request, CancellationToken cancellationToken)
    {
        var graph = await _codebaseAnalysisService.AnalyzeAsync(request, cancellationToken);
        var simplifiedGraph = _codeGraphSimplifier.Simplify(graph);

        return Ok(simplifiedGraph);
    }
}
