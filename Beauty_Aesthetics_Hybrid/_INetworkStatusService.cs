namespace SenangRetails.Shared.Services.Connectivity
{
    public interface INetworkStatusService
    {
        bool IsInternetAvailable { get; }
    }

    public sealed class DefaultNetworkStatusService : INetworkStatusService
    {
        public bool IsInternetAvailable => true;
    }
}
