using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Models;

public sealed class CustomerBalanceSnapshot
{
    public MemberBalanceSummaryDTO Summary { get; init; } = new();
    public IReadOnlyList<PackageBalanceDetailDTO> Packages { get; init; } = Array.Empty<PackageBalanceDetailDTO>();
    public IReadOnlyList<CreditBalanceDetailDTO> Credits { get; init; } = Array.Empty<CreditBalanceDetailDTO>();
}