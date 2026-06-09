namespace Glasstrut.Api.Models.Dtos;

public record PrizeRedeemResponse(
    Guid Id,
    string PrizeDescription,
    decimal Cost,
    string? Notes,
    DateTime ClaimedAt
);

public record PrizeClaimDto(
    Guid Id,
    string PrizeDescription,
    decimal? Cost,
    string UserEmail,
    string? Notes,
    DateTime ClaimedAt
);
