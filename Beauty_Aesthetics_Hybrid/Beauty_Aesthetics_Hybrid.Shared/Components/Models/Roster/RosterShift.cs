using System;
using System.Collections.Generic;

namespace Beauty_Aesthetics_WebPos.Components.Models
{
    public class BreakPeriod
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }

    public class RosterShift
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Employee reference (use SystemID or Id depending on your Employee model)
        public string EmployeeId { get; set; } = "";

        // Branch identifier for filtering
        public string BranchId { get; set; } = "";

        // Date of the shift (only date portion used, time held separately)
        public DateTime Date { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsOvernight { get; set; } = false;

        // Repeat weekly or not
        public bool RepeatsWeekly { get; set; } = false;
        public DateTime? RepeatEndDate { get; set; }

        public List<BreakPeriod> Breaks { get; set; } = new();
        public string Remarks { get; set; } = "";

        // Display helpers (not persisted specifically)
        public string DisplayTime => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    }
}
