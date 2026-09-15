using SenangRetails.Shared.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.BranchService
{
    public interface IBranchService
    {
        Task<(BranchItem? item, string message)> GetBranchDetailsAsync(string branchId);
        Task<(bool success, string message)> UpdateBranchAsync(BranchItem item);
    }
}
