using Commerce.Api.Auth;
using Commerce.Application.Models;
using Commerce.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/commerce")]
public sealed class CommerceController(CommerceService service) : ControllerBase
{
    /// <summary>Loads a commerce_DDMMYYYY.csv file. 400 if it is empty or invalid, 409 if keys already exist.</summary>
    [HttpPost("files")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadFile(IFormFile? file, CancellationToken cancellationToken)
    {
        byte[]? content = null;

        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        var result = await service.UploadAsync(file?.FileName, content, this.ToAuditContext(), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Moves the invalid records of a date to quarantine and returns how many moved.</summary>
    [HttpPost("process")]
    [ProducesResponseType<ProcessResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessResult>> Process([FromBody] ProcessRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ProcessAsync(request.ProcessDate, this.ToAuditContext(), cancellationToken);

        return Ok(result);
    }

    /// <summary>Lists the records in the commerce table. The date filter is optional.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommerceRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CommerceRecord>>> List(
        [FromQuery] string? processDate,
        CancellationToken cancellationToken)
    {
        var records = await service.ListAsync(processDate, cancellationToken);

        return Ok(records);
    }

    /// <summary>Lists quarantined records with their motivo. The date filter is optional.</summary>
    [HttpGet("quarantine")]
    [ProducesResponseType<IReadOnlyList<QuarantineRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<QuarantineRecord>>> ListQuarantine(
        [FromQuery] string? processDate,
        CancellationToken cancellationToken)
    {
        var records = await service.ListQuarantineAsync(processDate, cancellationToken);

        return Ok(records);
    }
}
