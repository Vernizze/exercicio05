using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Questao5.Application.Exceptions;

namespace Questao5.Tests.Infrastructure.Services;

[ApiController]
[Route("security-tests")]
public sealed class SecurityTestController : ControllerBase
{
    [HttpGet("success")]
    public ActionResult<object> Success()
    {
        return Ok(new { correlationId = HttpContext.TraceIdentifier });
    }

    [HttpPost("validation")]
    public ActionResult Validation(SecurityTestRequest request)
    {
        return NoContent();
    }

    [HttpGet("business-error")]
    public ActionResult BusinessError()
    {
        _ = HttpContext.TraceIdentifier;
        throw new BusinessRuleException("TEST_BUSINESS_RULE", "A regra de negócio não foi atendida.");
    }

    [HttpGet("unexpected-error")]
    public ActionResult UnexpectedError()
    {
        _ = HttpContext.TraceIdentifier;
        throw new InvalidOperationException(
            "sensitive-value; Data Source=C:\\private\\database.sqlite; SELECT * FROM conta");
    }
}

public sealed record SecurityTestRequest(
    [Required, StringLength(10, MinimumLength = 1)] string? Value);