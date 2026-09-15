using Beauty_Aesthetics_WebPos.Models;
using Microsoft.Extensions.Logging;

namespace Beauty_Aesthetics_WebPos.Data;

public sealed class UnavailableRatingService : IRatingService
{
    private readonly ILogger<UnavailableRatingService> logger;

    public UnavailableRatingService(ILogger<UnavailableRatingService> logger)
    {
        this.logger = logger;
    }

    public Task SaveRatingAsync(CustomerRating rating)
    {
        logger.LogWarning("Customer rating storage is unavailable because no database connection string is configured.");
        throw new InvalidOperationException("Customer rating storage is not configured.");
    }
}
