using Microsoft.Maui.Networking;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Services
{
    public sealed class MauiNetworkStatusService : INetworkStatusService
    {
        public bool IsInternetAvailable => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }
}
