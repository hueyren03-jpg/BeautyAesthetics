using Beauty_Aesthetic_WebPos.Data;
using Beauty_Aesthetics_WebPos.Models;
using Microsoft.Extensions.Logging;

namespace Beauty_Aesthetics_WebPos.Data
{
    public interface IRatingService
    {
        Task SaveRatingAsync(CustomerRating rating);
    }

    public class RatingService : IRatingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RatingService> _logger;

        public RatingService(
            ApplicationDbContext context,
            ILogger<RatingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SaveRatingAsync(CustomerRating rating)
        {
            try
            {
                // Add to context and save
                _context.CustomerRatings.Add(rating);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Rating saved successfully for {AccountName}", rating.AccountName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving rating");
                throw;
            }
        }
    }
}