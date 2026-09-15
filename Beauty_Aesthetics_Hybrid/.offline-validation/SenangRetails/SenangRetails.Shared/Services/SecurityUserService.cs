using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services
{
    public class SecurityUserService
    {
        private readonly SecurityUserAC _ac;
        private readonly AppState _appState;

        public SecurityUserService(SecurityUserAC ac, AppState appState)
        {
            _ac       = ac;
            _appState = appState;
        }

        public bool CanView(string formName)   => Check(formName, p => p.CanViewRecord);
        public bool CanAdd(string formName)    => Check(formName, p => p.CanAddRecord);
        public bool CanEdit(string formName)   => Check(formName, p => p.CanEditRecord);
        public bool CanDelete(string formName) => Check(formName, p => p.CanDeleteRecord);

        private bool Check(string formName, Func<SecurityPermissionItem, bool> selector)
        {
            if (_appState.Permissions == null) return true;
            foreach (var kv in _appState.Permissions)
                if (string.Equals(kv.Key, formName, StringComparison.OrdinalIgnoreCase))
                    return selector(kv.Value);
            return true;
        }

        public async Task<bool> ReloadPermissionsAsync(string email)
        {
            var result = await _ac.GetUserByEmailAsync(email);
            if (result?.result == null) return false;

            var u = result.result;
            _appState.UserID                 = u.UserID;
            _appState.EmployeeID             = u.EmployeeID;
            _appState.UserGroupID            = u.UserGroupID;
            _appState.UserGroupName          = u.UserGroupName;
            _appState.BranchGroupID          = u.BranchGroupID;
            _appState.DefaultWorkingBranchID = u.DefaultWorkingBranchID;
            _appState.UserEmail              = email;
            _appState.Permissions            = u.LstSecurities;
            return true;
        }

        public async Task<(bool ok, string message)> SavePermissionsAsync(Dictionary<string, SecurityPermissionItem> permissions)
        {
            var dm = new SecurityUserUpdateDM
            {
                UserID                 = _appState.UserID,
                EmployeeID             = _appState.EmployeeID,
                UserGroupID            = _appState.UserGroupID,
                UserGroupName          = _appState.UserGroupName,
                BranchGroupID          = _appState.BranchGroupID,
                BranchID               = _appState.SelectedBranchID,
                DefaultWorkingBranchID = _appState.DefaultWorkingBranchID,
                LstSecurities          = permissions,
                SaveAction             = 2,
                IsDirty                = true
            };

            var result = await _ac.UpdateRecordAsync(dm);
            if (result?.statusCode == 200)
            {
                _appState.Permissions = permissions;
                return (true, result.message ?? "");
            }
            return (false, result?.message ?? "No response from server.");
        }
    }
}
