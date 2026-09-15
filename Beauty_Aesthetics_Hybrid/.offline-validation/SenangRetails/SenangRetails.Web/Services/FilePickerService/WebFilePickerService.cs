using SenangRetails.Shared.Services.FilePickerService;

namespace SenangRetails.Web.Services.FilePickerService
{
    public class WebFilePickerService : IFilePickerService
    {
        private TaskCompletionSource<FilePickerResult?>? _tcs;

        public Task<FilePickerResult?> PickFileAsync(string[] allowedExtensions)
        {
            _tcs = new TaskCompletionSource<FilePickerResult?>();
            return _tcs.Task;
        }

        public void Complete(FilePickerResult? result)
        {
            _tcs?.TrySetResult(result);
        }
    }
}
