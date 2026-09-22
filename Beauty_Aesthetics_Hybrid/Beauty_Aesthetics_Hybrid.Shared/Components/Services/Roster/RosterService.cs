using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Beauty_Aesthetics_WebPos.Components.Models;

namespace Beauty_Aesthetics_WebPos.Components.Services
{
    public class RosterService
    {
        private readonly string dataFolder;
        private readonly string rosterFile;
        private readonly object _lock = new();
        private List<RosterShift> shifts = new();

        public RosterService()
        {
            string folder;
            try
            {
                folder = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                Directory.CreateDirectory(folder);
            }
            catch
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeautyAesthetics", "Data");
                Directory.CreateDirectory(folder);
            }
            dataFolder = folder;

            rosterFile = Path.Combine(dataFolder, "roster.json");

            if (!File.Exists(rosterFile))
            {
                File.WriteAllText(rosterFile, JsonSerializer.Serialize(new List<RosterShift>()));
            }

            Load();
        }

        private void Load()
        {
            lock (_lock)
            {
                try
                {
                    var json = File.ReadAllText(rosterFile);
                    shifts = JsonSerializer.Deserialize<List<RosterShift>>(json) ?? new List<RosterShift>();
                }
                catch
                {
                    shifts = new List<RosterShift>();
                }
            }
        }

        private void Save()
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(shifts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(rosterFile, json);
            }
        }

        // Get all shifts for a week and branch
        public Task<List<RosterShift>> GetShiftsForWeekAsync(string branchId, DateTime weekStart)
        {
            // weekStart should be the Monday (or chosen start) of the week
            var start = weekStart.Date;
            var end = start.AddDays(7).Date;

            var result = shifts
                .Where(s => s.Date.Date >= start && s.Date.Date < end)
                .Where(s => string.IsNullOrEmpty(branchId) || s.BranchId == branchId)
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                .ToList();

            return Task.FromResult(result);
        }

        // Get all shifts for a specific date range (used by month view)
        public Task<List<RosterShift>> GetShiftsForDateRangeAsync(string branchId, DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1); // exclusive upper bound

            var result = shifts
                .Where(s => s.Date.Date >= start && s.Date.Date < end)
                .Where(s => string.IsNullOrEmpty(branchId) || s.BranchId == branchId)
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<RosterShift?> GetShiftByIdAsync(Guid id) =>
            Task.FromResult(shifts.FirstOrDefault(s => s.Id == id));

        public Task AddOrUpdateShiftAsync(RosterShift shift)
        {
            lock (_lock)
            {
                // Remove existing shift with same ID if editing
                shifts.RemoveAll(s => s.Id == shift.Id);

                // --- Generate Recurring Shifts if needed ---
                if (shift.RepeatsWeekly && shift.RepeatEndDate.HasValue && shift.RepeatEndDate.Value > shift.Date)
                {
                    DateTime currentDate = shift.Date;
                    while (currentDate <= shift.RepeatEndDate.Value)
                    {
                        var newShift = new RosterShift
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = shift.EmployeeId,
                            BranchId = shift.BranchId,
                            Date = currentDate,
                            StartTime = shift.StartTime,
                            EndTime = shift.EndTime,
                            IsOvernight = shift.IsOvernight,
                            RepeatsWeekly = false, // prevent recursion
                            RepeatEndDate = null,
                            Breaks = shift.Breaks.Select(b => new BreakPeriod { Start = b.Start, End = b.End }).ToList(),
                            Remarks = shift.Remarks
                        };

                        shifts.Add(newShift);
                        currentDate = currentDate.AddDays(7); // every week, same day
                    }
                }
                else
                {
                    // Normal single shift
                    shifts.Add(shift);
                }

                Save();
            }

            return Task.CompletedTask;
        }


        public Task DeleteShiftAsync(Guid id)
        {
            shifts.RemoveAll(s => s.Id == id);
            Save();
            return Task.CompletedTask;
        }

        // Convenience: get distinct branches from existing shifts (you may replace with real branch source)
        public Task<List<string>> GetBranchesAsync()
        {
            var branches = shifts.Select(s => s.BranchId).Where(b => !string.IsNullOrEmpty(b)).Distinct().ToList();
            return Task.FromResult(branches);
        }

        // Optional: seed helper if you want initial demo shifts
        public Task SeedDemoDataAsync()
        {
            if (!shifts.Any())
            {
                var today = DateTime.Today;
                var monday = today.AddDays(-(int)today.DayOfWeek + 1); // assuming Monday start
                shifts.Add(new RosterShift
                {
                    EmployeeId = "EMP001",
                    BranchId = "HQMA",
                    Date = monday,
                    StartTime = new TimeSpan(9, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    Remarks = "Demo shift"
                });
                Save();
            }
            return Task.CompletedTask;
        }
    }
}
