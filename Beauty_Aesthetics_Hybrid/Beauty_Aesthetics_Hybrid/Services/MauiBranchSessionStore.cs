using Beauty_Aesthetics_WebPos.Components.Services.Branches;
using Microsoft.Maui.Storage;

namespace Beauty_Aesthetics_Hybrid.Services;

public sealed class MauiBranchSessionStore : IBranchSessionStore
{
    private const string BranchIdKey = "session.branch_id";

    public async Task SaveBranchIdAsync(string branchId)
    {
        try
        {
            await SecureStorage.Default.SetAsync(BranchIdKey, branchId);
        }
        catch
        {
            // Secure storage can be unavailable on an unsupported device profile.
        }
    }

    public async Task<string?> GetBranchIdAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(BranchIdKey);
        }
        catch
        {
            return null;
        }
    }

    public Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(BranchIdKey);
        }
        catch
        {
        }

        return Task.CompletedTask;
    }
}
