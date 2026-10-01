using MediatR;
using Microsoft.AspNetCore.Mvc;
using Questao5.Application.Commands.Requests;
using Questao5.Infrastructure.Services.Controllers.Filters;
using Questao5.Infrastructure.Services.Controllers.Models;

namespace Questao5.Infrastructure.Services.Controllers
{
    [ApiController]
    [Route("api/v1/movimentos")]
    public sealed class MovementController : ControllerBase
    {
        private readonly IMediator mediator;

        public MovementController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType<CreateMovementHttpResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
        [RequestSizeLimit(MovementRequestBodySizeLimitAttribute.MaximumBodySize)]
        [MovementRequestBodySizeLimit]
        public async Task<ActionResult<CreateMovementHttpResponse>> Create(
            [FromBody] CreateMovementRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateMovementCommand(
                request.IdRequisicao!,
                request.IdContaCorrente!,
                request.Valor!.Value,
                request.TipoMovimento!);
            var response = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            return Ok(new CreateMovementHttpResponse(response.MovementId));
        }
    }
}