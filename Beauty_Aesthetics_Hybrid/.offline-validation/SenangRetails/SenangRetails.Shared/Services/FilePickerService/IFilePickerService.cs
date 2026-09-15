using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.FilePickerService
{
    public interface IFilePickerService
    {
        Task<FilePickerResult?> PickFileAsync(string[] allowedExtensions);
    }

    public class FilePickerResult
    {
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = "";
        public MemoryStream Stream { get; set; } = new();
    }
}
