using AiCodeAssistant.Application.Services; 
using AiCodeAssistant.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiCodeAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GraphController : ControllerBase
{
    private readonly IGraphDataService _graphDataService;

    public GraphController(IGraphDataService graphDataService)
    {
        _graphDataService = graphDataService;
    }

    [HttpGet("nodes")]
    public IActionResult GetNodes()
    {
        var nodes = _graphDataService.GetNodes();
        return Ok(nodes);
    }

    [HttpGet("edges")]
    public IActionResult GetEdges()
    {
        var edges = _graphDataService.GetEdges();
        return Ok(edges);
    }

    [HttpGet("flows")]
    public IActionResult GetFlows()
    {
        var flows = _graphDataService.GetFlows();
        return Ok(flows);
    }

    [HttpGet("endpoints")]
    public IActionResult GetEndpoints()
    {
        var endpoints = _graphDataService.GetEndpoints();
        return Ok(endpoints);
    }
}
