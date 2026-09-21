using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Models;

namespace Beauty_Aesthetics_WebPos.Components.Pages
{
    public partial class CustomerRatingPage
    {
        private int rating = 0;
        private int hoverRating = 0;
        private string comment = "";
        private bool hasSubmitted = false;
        private bool isSaving = false;
        private string errorMessage = string.Empty;
        private string GoogleReviewUrl => Configuration["GoogleReviewUrl"]!; // Get from GoogleReviewUrl at appsettings.json

        private string MasterAccountID { get; set; } = "CUST12345"; // Hardcode Customer ID for testing
        private string AccountName { get; set; } = "Test Cust"; // Harcode Customer Name for testing

        [Inject] private Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> L { get; set; } = default!;

        private void OnRate(int value)
        {
            if (isSaving) return;
            rating = value;
            errorMessage = string.Empty;
        }

        private void ResetRating()
        {
            if (isSaving) return;
            rating = 0;
            comment = "";
            hoverRating = 0;
            errorMessage = string.Empty;
        }

        private void OnCommentInput(ChangeEventArgs e)
        {
            if (isSaving) return;
            var value = e.Value?.ToString() ?? "";
            if (value.Length > 500)
            {
                comment = value.Substring(0, 500);
            }
            else
            {
                comment = value;
            }
        }

        private async Task RedirectToGoogleReview()
        {
            if (isSaving) return;

            isSaving = true;
            errorMessage = string.Empty;

            try
            {
                // Save 5-star rating to database
                var ratingModel = new CustomerRating
                {
                    MasterAccountID = MasterAccountID,
                    AccountName = AccountName,
                    Rating = 5,
                    IsGoogleReviewRedirected = true
                };

                await RatingService.SaveRatingAsync(ratingModel);

                hasSubmitted = true;

                // Small delay for better UX
                await Task.Delay(800);

                // Redirect to Google Reviews
                NavigationManager.NavigateTo(GoogleReviewUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                errorMessage = L["SaveRatingError"];
                Console.WriteLine($"Error saving rating: {ex.Message}");
            }
            finally
            {
                isSaving = false;
            }
        }

        private async Task Submit()
        {
            if (string.IsNullOrWhiteSpace(comment) || isSaving)
                return;

            isSaving = true;
            errorMessage = string.Empty;

            try
            {
                var ratingModel = new CustomerRating
                {
                    MasterAccountID = MasterAccountID,
                    AccountName = AccountName,
                    Rating = rating,
                    Comment = comment
                };

                await RatingService.SaveRatingAsync(ratingModel);
                hasSubmitted = true;
            }
            catch (Exception ex)
            {
                errorMessage = L["SubmitFeedbackError"];
                Console.WriteLine($"Error submitting feedback: {ex.Message}");
            }
            finally
            {
                isSaving = false;
            }
        }

        private string MessageText => rating switch
        {
            1 => L["RatingMessage1"],
            2 => L["RatingMessage2"],
            3 => L["RatingMessage3"],
            4 => L["RatingMessage4"],
            5 => L["RatingMessage5"],
            _ => ""
        };

        private string GetFeedbackTitle() => rating switch
        {
            1 => L["RatingTitle1"],
            2 => L["RatingTitle2"],
            3 => L["RatingTitle3"],
            4 => L["RatingTitle4"],
            5 => L["RatingTitle5"],
            _ => ""
        };

        private string GetFeedbackIcon() => rating switch
        {
            1 => "🙏",
            2 => "💭",
            3 => "🙂",
            4 => "😊",
            5 => "🤩",
            _ => "⭐"
        };

        private string GetMessageClass() => rating switch
        {
            1 => "rating-1",
            2 => "rating-2",
            3 => "rating-3",
            4 => "rating-4",
            5 => "rating-5",
            _ => ""
        };

        private void ResetForm()
        {
            rating = 0;
            comment = "";
            hoverRating = 0;
            hasSubmitted = false;
            isSaving = false;
            errorMessage = string.Empty;
        }
    }
}
