using Microsoft.JSInterop;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace SenangRetails.Shared.Services.AuthService
{
    public class AuthService : IAuthService
    {
        private readonly AuthAC _authAC;
        private readonly IStoreTokenService _storeTokenService;
        private readonly AppState _appState;
        private readonly IJSRuntime _js;
        public AuthService(AuthAC authAC, IStoreTokenService storeTokenService, AppState appState, IJSRuntime js)
        {
            _authAC = authAC;
            _storeTokenService = storeTokenService;
            _appState = appState;
            _js = js;
        }

        public async Task<bool> AuthenticateAsync(string email, string password)
        {
            var response = await _authAC.LoginAsync(email, password);

            if (response == null)
                return false;

            if (response.result == null)
                return false;

            ApiAuthResult authResult = response.result;


            if (response.statusCode == 200)
            {
                Debug.WriteLine($"Access Token: {authResult.AuthResponse.AccessToken}");
                await _storeTokenService.SaveTokenAsync(authResult.AuthResponse.AccessToken, authResult.AuthResponse.RefreshToken);
            }

            _authAC.CreateBearerAuthAsync(authResult.AuthResponse.AccessToken);

            var userDetails = await _authAC.GetUserByEmailAsync(email);

            if (userDetails?.result != null)
            {
                var u = userDetails.result;

                var branches = u.BranchID
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

                _appState.AvailableBranches = branches;
                _appState.UserID = u.UserID;
                _appState.EmployeeID = u.EmployeeID;
                _appState.UserGroupID = u.UserGroupID;
                _appState.UserGroupName = u.UserGroupName;
                _appState.BranchGroupID = u.BranchGroupID;
                _appState.DefaultWorkingBranchID = u.DefaultWorkingBranchID;
                _appState.UserEmail = email;
                _appState.Permissions = u.LstSecurities;

                await _js.InvokeVoidAsync("localStorage.setItem", "available_branches", JsonSerializer.Serialize(branches));
            }

            return true;


        }
    }
}
