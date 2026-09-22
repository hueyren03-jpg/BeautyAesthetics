using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Components.Services.Branches;

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
        private string GoogleReviewUrl => Configuration["GoogleReviewUrl"] ?? string.Empty;

        [Parameter]
        [SupplyParameterFromQuery(Name = "customerId")]
        public string? CustomerId { get; set; }

        [Parameter]
        [SupplyParameterFromQuery(Name = "documentId")]
        public string? DocumentId { get; set; }

        [Parameter]
        [SupplyParameterFromQuery(Name = "branchId")]
        public string? BranchId { get; set; }

        [Parameter]
        [SupplyParameterFromQuery(Name = "groupId")]
        public string? GroupId { get; set; }

        [Inject] private Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> L { get; set; } = default!;
        [Inject] private AppFeedbackService Feedback { get; set; } = default!;
        [Inject] private IBranchSessionService BranchSession { get; set; } = default!;

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
            var feedbackId = Feedback.Loading("Saving your rating...", "Saving rating");

            try
            {
                var shouldRedirectToGoogle = !string.IsNullOrWhiteSpace(GoogleReviewUrl);
                if (!TryCreateRating(5, null, shouldRedirectToGoogle, out var ratingModel))
                {
                    Feedback.Fail(feedbackId, errorMessage, "Rating not saved");
                    return;
                }

                var result = await RatingService.SaveRatingAsync(ratingModel);
                if (!result.Success)
                {
                    errorMessage = result.ErrorMessage ?? L["SaveRatingError"].Value;
                    Feedback.Fail(feedbackId, errorMessage, "Rating not saved");
                    return;
                }

                hasSubmitted = true;
                Feedback.Resolve(feedbackId, "Your 5-star rating was saved successfully.", "Rating saved");

                if (shouldRedirectToGoogle)
                {
                    await Task.Delay(800);
                    NavigationManager.NavigateTo(GoogleReviewUrl, forceLoad: true);
                }
            }
            catch (Exception ex)
            {
                errorMessage = L["SaveRatingError"];
                Feedback.Fail(feedbackId, errorMessage, "Rating not saved");
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

            if (rating <= 0)
            {
                Feedback.Warning("Select a rating before submitting your feedback.", "Rating required");
                return;
            }

            isSaving = true;
            errorMessage = string.Empty;
            var feedbackId = Feedback.Loading("Submitting your feedback...", "Submitting feedback");

            try
            {
                if (!TryCreateRating(rating, comment, false, out var ratingModel))
                {
                    Feedback.Fail(feedbackId, errorMessage, "Feedback not submitted");
                    return;
                }

                var result = await RatingService.SaveRatingAsync(ratingModel);
                if (!result.Success)
                {
                    errorMessage = result.ErrorMessage ?? L["SubmitFeedbackError"].Value;
                    Feedback.Fail(feedbackId, errorMessage, "Feedback not submitted");
                    return;
                }

                hasSubmitted = true;
                Feedback.Resolve(feedbackId, "Thank you. Your feedback was submitted successfully.", "Feedback submitted");
            }
            catch (Exception ex)
            {
                errorMessage = L["SubmitFeedbackError"];
                Feedback.Fail(feedbackId, errorMessage, "Feedback not submitted");
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

        private bool TryCreateRating(
            int ratingValue,
            string? comment,
            bool isGoogleReviewRedirected,
            out CustomerRating ratingModel)
        {
            var customerId = CustomerId?.Trim() ?? string.Empty;
            var documentId = DocumentId?.Trim() ?? string.Empty;
            var branchId = !string.IsNullOrWhiteSpace(BranchId)
                ? BranchId.Trim()
                : BranchSession.CurrentBranch?.Id?.Trim() ?? string.Empty;
            var groupId = !string.IsNullOrWhiteSpace(GroupId)
                ? GroupId.Trim()
                : branchId;

            ratingModel = new CustomerRating();

            if (string.IsNullOrWhiteSpace(customerId) ||
                string.IsNullOrWhiteSpace(documentId) ||
                string.IsNullOrWhiteSpace(branchId))
            {
                errorMessage = "This review is missing its customer, sales document, or branch information. Open it from a completed sale and try again.";
                return false;
            }

            ratingModel = new CustomerRating
            {
                RatingID = string.Empty,
                CustomerID = customerId,
                DocumentID = documentId,
                Rating = Math.Clamp(ratingValue, 1, 5),
                Comment = string.IsNullOrWhiteSpace(comment) ? string.Empty : comment.Trim(),
                SubmissionDate = DateTime.UtcNow,
                IsGoogleReviewRedirected = isGoogleReviewRedirected,
                BranchID = branchId,
                GroupID = groupId,
                SaveAction = "Added",
                IsDirty = true
            };

            return true;
        }

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
