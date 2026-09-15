using System;
using System.Collections.Generic;
using System.Text;
using EBI.DM;

namespace SenangRetails.Shared.Models.DTOs
{
    public class PointConversionResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public PointConversionResult? Result { get; set; }
    }

    public class PointConversionResult
    {
        public string Id { get; set; } = string.Empty;
        public string? DisplayCode { get; set; }
        public string SuccessMessage { get; set; } = string.Empty;
    }
}
