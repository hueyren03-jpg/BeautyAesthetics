using Beauty_Aesthetics_WebPos.Models;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Beauty_Aesthetics_WebPos.Data;

public sealed class UnavailableRatingService : IRatingService
{
    private readonly ILogger<UnavailableRatingService> logger;

    public UnavailableRatingService(ILogger<UnavailableRatingService> logger)
    {
        this.logger = logger;
    }

    public Task<ApiCallResult<bool>> SaveRatingAsync(
        CustomerRating rating,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Customer rating API service is unavailable.");
        return Task.FromResult(ApiCallResult<bool>.Failure(
            HttpStatusCode.ServiceUnavailable,
            "Customer rating service is unavailable."));
    }
}
