using BlazorBootstrap;
using CsvHelper;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services
{
    public class StaffService
    {
        private readonly StaffAC _staffAC;
        private readonly AppState _appState;

        public StaffService(StaffAC staffAC, AppState appState)
        {
            _staffAC = staffAC;
            _appState = appState;
        }

        public async Task<ApiResponse<List<StaffResponseDTO>>> GetStaffListAsync()
        {
            return await _staffAC.FetchStaffListAsync();
        }

        public async Task<ApiResponseRoot<string>> CreateStaffAsync(StaffRequestDTO payload)
        {
            payload.BranchId = _appState.SelectedBranchID;
            //payload.DateResigned = null;
            payload.MasterAccountId = null;
            payload.CreatedDateTime = DateTime.Now;
            //payload.DateHired = null;
            payload.SaveAction = "Added";
            return await _staffAC.CreateStaffAsync(payload);
        }

        public async Task<ApiResponseRoot<string>> EditStaffAsync(StaffRequestDTO payload)
        {

            // 1. Assign variables correctly similar to Create
            payload.BranchId = _appState.SelectedBranchID;

            // 2. Set Save Action to "Changed" as requested
            payload.SaveAction = "Changed";

            return await _staffAC.PutStaffAsync(payload);
        }

        public async Task<byte[]> ExportStaffToCsvAsync()
        {
            // 1. Get the latest staff list
            var response = await GetStaffListAsync();
            var staffData = response?.Result ?? new List<StaffResponseDTO>();

            // 2. Filter and Map only the fields requested in StaffRequestDTO
            var exportData = staffData.Select(s => new
            {
                s.MasterAccountID,
                s.AccountStatus,
                s.AccountName,
                s.SalesPersonCode,
                s.IsSalesPerson,
                s.IsNotSalesPerson,
                s.JobTitle,
                s.Phone,
                s.Gender,
                s.BirthdayYear,
                s.BirthdayMonth,
                s.BirthdayDay,
                s.DateHired,
                s.DateResigned
            }).ToList();

            // 3. Use a MemoryStream to write the CSV data
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // 4. Write the projected records
                await csv.WriteRecordsAsync(exportData);
                await writer.FlushAsync();
            }

            return memoryStream.ToArray();
        }
        public async Task<(int SuccessCount, int ErrorCount, List<string> Errors)> ImportStaffFromCsvAsync(Stream fileStream)
        {
            int successCount = 0;
            int errorCount = 0;
            var errorMessages = new List<string>();

            try
            {
                if (fileStream.CanSeek) fileStream.Position = 0;

                using var reader = new StreamReader(fileStream, Encoding.UTF8);
                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    TrimOptions = CsvHelper.Configuration.TrimOptions.Trim,
                    // Add this line:
                    ShouldSkipRecord = args => args.Row.Parser.Record.All(string.IsNullOrWhiteSpace)
                };

                using var csv = new CsvReader(reader, config);



                // --- ADD THESE LINES TO HANDLE THE NULL CONVERSION ERROR ---
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTimeOffset?>().NullValues.Add("");
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTimeOffset?>().NullValues.Add("01/01/0001 00:00:00");
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTimeOffset?>().NullValues.Add("0001-01-01T00:00:00+00:00");

                // This ensures CsvHelper only looks for the headers that actually exist in your file
                csv.Context.RegisterClassMap<StaffImportMap>();

                var records = csv.GetRecords<StaffRequestDTO>().ToList();

                if (!records.Any())
                {
                    errorMessages.Add("The CSV file contains no data rows.");
                    return (0, 0, errorMessages);
                }

                // Define a safe minimum date for SQL Server (1753-01-01)
                DateTime sqlMinDate = new DateTime(1753, 1, 1);

                foreach (var staffDto in records)
                {
                    try
                    {
                        // 1. Basic Validation
                        if (string.IsNullOrWhiteSpace(staffDto.AccountName) || string.IsNullOrWhiteSpace(staffDto.Phone))
                        {
                            errorCount++;
                            errorMessages.Add($"Row {successCount + errorCount}: Skipping - Name and Phone are mandatory.");
                            continue;
                        }


                        // 3. API Call
                        var result = await CreateStaffAsync(staffDto);

                        if (result != null && result.statusCode == 200)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errorMessages.Add($"Row {successCount + errorCount} ({staffDto.AccountName}): {result?.message ?? "API rejected the record"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errorMessages.Add($"Row {successCount + errorCount}: Unexpected error - {ex.Message}");
                    }
                }
            }
            catch (CsvHelperException ex)
            {
                errorMessages.Add($"CSV Format Error: Please ensure headers match the template. Details: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorMessages.Add($"Critical processing error: {ex.Message}");
            }

            return (successCount, errorCount, errorMessages);
        }

        public sealed class StaffImportMap : CsvHelper.Configuration.ClassMap<StaffRequestDTO>
        {
            public StaffImportMap()
            {
                // Map based on the exact names in your exported CSV image
                Map(m => m.MasterAccountId).Name("MasterAccountID");
                Map(m => m.AccountStatus).Name("AccountStatus");
                Map(m => m.AccountName).Name("AccountName");
                Map(m => m.SalesPersonCode).Name("SalesPersonCode");
                Map(m => m.IsSalesPerson).Name("IsSalesPerson");
                Map(m => m.IsNotSalesPerson).Name("IsNotSalesPerson");
                Map(m => m.JobTitle).Name("JobTitle");
                Map(m => m.Phone).Name("Phone");
                Map(m => m.Gender).Name("Gender");
                Map(m => m.BirthdayYear).Name("BirthdayYear");
                Map(m => m.BirthdayMonth).Name("BirthdayMonth");
                Map(m => m.BirthdayDay).Name("BirthdayDay");
                Map(m => m.DateHired).Name("DateHired");
                Map(m => m.DateResigned).Name("DateResigned");
            }
        }
    }
}