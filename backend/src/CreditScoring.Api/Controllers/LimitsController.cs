using CreditScoring.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/limits")]
public class LimitsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<LimitsResponse>(StatusCodes.Status200OK)]
    public LimitsResponse Get() => Limits.Current();
}
