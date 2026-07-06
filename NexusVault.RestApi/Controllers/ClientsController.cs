using Grpc.Core;
using NexusVault.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NexusVault.RestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly ClientManagerService.ClientManagerServiceClient _client;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        ClientManagerService.ClientManagerServiceClient client,
        ILogger<ClientsController> logger)
    {
        _client = client;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterClient([FromBody] RegisterClientRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.RegisterClientAsync(request, cancellationToken: cancellationToken);
            return Ok(response.Client);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(ex.Status.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering client");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> FetchClient(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.FetchClientAsync(new FetchClientRequest { ClientId = id }, cancellationToken: cancellationToken);
            return Ok(response.Client);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound($"Client {id} could not be found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching client {ClientId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
