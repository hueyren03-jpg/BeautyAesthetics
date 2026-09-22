using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models;
using Microsoft.Extensions.Logging;

namespace Beauty_Aesthetics_WebPos.Data
{
    public interface IRatingService
    {
        Task<ApiCallResult<bool>> SaveRatingAsync(
            CustomerRating rating,
            CancellationToken cancellationToken = default);
    }

    public class RatingService : IRatingService
    {
        private readonly CustomerRatingAC customerRatingAC;
        private readonly ILogger<RatingService> logger;

        public RatingService(
            CustomerRatingAC customerRatingAC,
            ILogger<RatingService> logger)
        {
            this.customerRatingAC = customerRatingAC;
            this.logger = logger;
        }

        public async Task<ApiCallResult<bool>> SaveRatingAsync(
            CustomerRating rating,
            CancellationToken cancellationToken = default)
        {
            var result = await customerRatingAC.CreateRecordAsync(rating, cancellationToken);
            if (result.Success)
            {
                logger.LogInformation(
                    "Customer rating saved through the API for customer {CustomerID} and document {DocumentID}",
                    rating.CustomerID,
                    rating.DocumentID);
            }
            else
            {
                logger.LogWarning(
                    "Customer rating API rejected the rating for customer {CustomerID}: {ErrorMessage}",
                    rating.CustomerID,
                    result.ErrorMessage);
            }

            return result;
        }
    }
}
