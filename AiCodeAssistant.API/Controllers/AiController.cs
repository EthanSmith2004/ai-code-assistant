using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiCodeAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly INodeExplanationService _nodeExplanationService;

    public AiController(INodeExplanationService nodeExplanationService)
    {
        _nodeExplanationService = nodeExplanationService;
    }

    [HttpPost("explain-node")]
    public async Task<ActionResult<ExplainNodeResponse>> ExplainNode(ExplainNodeRequest request)
    {
        var response = await _nodeExplanationService.ExplainNodeAsync(request);
        return Ok(response);
    }

    [HttpPost("explain-flow")]
    public async Task<ActionResult<ExplainFlowResponse>> ExplainFlow(ExplainFlowRequest request)
    {
        var response = await _nodeExplanationService.ExplainFlowAsync(request);
        return Ok(response);
    }

    [HttpPost("explain-endpoint")]
    public async Task<ActionResult<ExplainEndpointResponse>> ExplainEndpoint(ExplainEndpointRequest request)
    {
        var response = await _nodeExplanationService.ExplainEndpointAsync(request);
        return Ok(response);
    }
}
