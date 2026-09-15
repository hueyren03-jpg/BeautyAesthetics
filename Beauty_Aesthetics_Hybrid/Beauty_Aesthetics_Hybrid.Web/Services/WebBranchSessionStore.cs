using System.Security.Cryptography;
using Beauty_Aesthetics_WebPos.Components.Services.Branches;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebBranchSessionStore(ProtectedLocalStorage storage) : IBranchSessionStore
{
    private const string BranchIdKey = "session.branch_id";

    public async Task SaveBranchIdAsync(string branchId)
    {
        try
        {
            await storage.SetAsync(BranchIdKey, branchId);
        }
        catch (InvalidOperationException)
        {
            // Browser storage is unavailable during prerendering.
        }
        catch (JSDisconnectedException)
        {
            // The browser circuit closed before the write completed.
        }
    }

    public async Task<string?> GetBranchIdAsync()
    {
        try
        {
            var result = await storage.GetAsync<string>(BranchIdKey);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await storage.DeleteAsync(BranchIdKey);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
