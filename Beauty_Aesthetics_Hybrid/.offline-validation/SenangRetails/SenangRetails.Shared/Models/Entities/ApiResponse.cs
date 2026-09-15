using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.Entities
{
    /// <summary>Non-generic interface so BaseAC can read/write statusCode and message without knowing T.</summary>
    public interface IApiResponse
    {
        int statusCode { get; set; }
        string message { get; set; }
    }

    public class ApiResponseRoot<T> : IApiResponse
    {
        public int statusCode { get; set; }
        public string message { get; set; } = string.Empty;
        public T? result { get; set; }
    }
}
