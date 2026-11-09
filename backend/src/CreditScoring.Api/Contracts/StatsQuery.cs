using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public sealed record StatsQuery
{
    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; init; }

    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; init; }

    [FromQuery(Name = "branchId")]
    public Guid? BranchId { get; init; }
}
