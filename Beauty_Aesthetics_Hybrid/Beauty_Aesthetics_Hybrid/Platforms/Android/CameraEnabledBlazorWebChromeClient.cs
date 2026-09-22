using Android.Content;
using Android.Webkit;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using File = Java.IO.File;
using Uri = Android.Net.Uri;

namespace Beauty_Aesthetics_Hybrid;

/// <summary>
/// Keeps the standard Blazor WebView link and file-picker behavior while
/// allowing the GRN scanner to request access to the Android camera.
/// </summary>
internal sealed class CameraEnabledBlazorWebChromeClient : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request is null)
        {
            return;
        }

        var resources = request.GetResources() ?? [];
        if (!resources.Any(resource =>
                string.Equals(resource, PermissionRequest.ResourceVideoCapture, StringComparison.OrdinalIgnoreCase)))
        {
            base.OnPermissionRequest(request);
            return;
        }

        RequestCameraPermissionAsync(request);
    }

    private static async void RequestCameraPermissionAsync(PermissionRequest request)
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (status == PermissionStatus.Granted)
                {
                    request.Grant([PermissionRequest.ResourceVideoCapture]);
                }
                else
                {
                    request.Deny();
                }
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(request.Deny);
        }
    }

    public override bool OnCreateWindow(
        global::Android.Webkit.WebView? view,
        bool isDialog,
        bool isUserGesture,
        Android.OS.Message? resultMsg)
    {
        var requestUrl = view?.GetHitTestResult()?.Extra;
        if (view?.Context is not null && !string.IsNullOrWhiteSpace(requestUrl))
        {
            view.Context.StartActivity(new Intent(Intent.ActionView, Uri.Parse(requestUrl)));
        }

        return false;
    }

    public override bool OnShowFileChooser(
        global::Android.Webkit.WebView? view,
        IValueCallback? filePathCallback,
        FileChooserParams? fileChooserParams)
    {
        if (filePathCallback is null)
        {
            return base.OnShowFileChooser(view, filePathCallback, fileChooserParams);
        }

        OpenFilePickerAsync(filePathCallback, fileChooserParams);
        return true;
    }

    private static async void OpenFilePickerAsync(
        IValueCallback filePathCallback,
        FileChooserParams? fileChooserParams)
    {
        try
        {
            var pickOptions = GetPickOptions(fileChooserParams);
            var fileResults = fileChooserParams?.Mode == ChromeFileChooserMode.OpenMultiple
                ? await FilePicker.PickMultipleAsync(pickOptions)
                : new[] { (await FilePicker.PickAsync(pickOptions))! };

            if (fileResults?.All(file => file is null) ?? true)
            {
                filePathCallback.OnReceiveValue(null);
                return;
            }

            var fileUris = new List<Uri>();
            foreach (var fileResult in fileResults)
            {
                if (fileResult is null)
                {
                    continue;
                }

                var androidUri = Uri.FromFile(new File(fileResult.FullPath));
                if (androidUri is not null)
                {
                    fileUris.Add(androidUri);
                }
            }

            filePathCallback.OnReceiveValue(fileUris.ToArray());
        }
        catch
        {
            filePathCallback.OnReceiveValue(null);
        }
    }

    private static PickOptions? GetPickOptions(FileChooserParams? fileChooserParams)
    {
        var acceptedFileTypes = fileChooserParams?.GetAcceptTypes();
        if (acceptedFileTypes is null ||
            (acceptedFileTypes.Length == 1 && string.IsNullOrEmpty(acceptedFileTypes[0])))
        {
            return null;
        }

        return new PickOptions
        {
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = acceptedFileTypes
            })
        };
    }
}
