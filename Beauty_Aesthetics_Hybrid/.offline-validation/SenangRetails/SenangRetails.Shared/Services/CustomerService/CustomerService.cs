using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.DM;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersBalanceSummary;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.Connectivity;
using static SenangRetails.Shared.ApiClient.CustomerAC;

namespace SenangRetails.Shared.Services.CustomerService
{
    public class CustomerService : ICustomerService
    {
        private readonly CustomerAC _customerAC;
        private readonly INetworkStatusService _network;

        public CustomerService(CustomerAC customerAc, INetworkStatusService network)
        {
            _customerAC = customerAc;
            _network = network;
        }

        public async Task<List<CustomerDM>> SearchCustomer(string keyword)
        {
            List<CustomerDM>? onlineCustomers = null;

            // 1. Try online fetch
            if (_network.IsInternetAvailable)
            {
                try
                {
                    onlineCustomers = await _customerAC.SearchCustomersAsync(keyword);
                    if (onlineCustomers != null && onlineCustomers.Count > 0)
                    {
                        await CacheCustomersToSqliteAsync(onlineCustomers);
                        return onlineCustomers;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CustomerService] Online search error: {ex.Message}. Falling back to SQLite.");
                }
            }

            // 2. Fallback to local SQLite database when offline or if online search returned empty/error
            var localCustomers = await SearchCustomersFromSqliteAsync(keyword);
            if (localCustomers != null && localCustomers.Count > 0)
            {
                return localCustomers;
            }

            return onlineCustomers ?? new List<CustomerDM>();
        }

        public async Task<CustomerDM> GetSingleCustomer(string keyword)
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var customer = await _customerAC.GetSingleCustomerAsync(keyword);
                    if (customer != null && !string.IsNullOrEmpty(customer.MasterAccountID))
                    {
                        await CacheCustomersToSqliteAsync(new List<CustomerDM> { customer });
                        return customer;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CustomerService] Online GetSingleCustomer error: {ex.Message}. Falling back to SQLite.");
                }
            }

            var localCustomer = await GetSingleCustomerFromSqliteAsync(keyword);
            return localCustomer ?? new CustomerDM();
        }

        private async Task<List<CustomerDM>> SearchCustomersFromSqliteAsync(string keyword)
        {
            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);

                IQueryable<LocalCustomerEntity> query = db.LocalCustomers;

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var kw = keyword.Trim();
                    query = query.Where(c =>
                        EF.Functions.Like(c.AccountName, $"%{kw}%") ||
                        EF.Functions.Like(c.Phone, $"%{kw}%") ||
                        EF.Functions.Like(c.MasterAccountId, $"%{kw}%") ||
                        EF.Functions.Like(c.NRIC, $"%{kw}%") ||
                        EF.Functions.Like(c.Email, $"%{kw}%"));
                }

                var entities = await query.OrderBy(c => c.AccountName).Take(100).ToListAsync();
                var result = new List<CustomerDM>();

                foreach (var e in entities)
                {
                    try
                    {
                        var cust = JsonSerializer.Deserialize<CustomerDM>(e.RawJson);
                        if (cust != null)
                        {
                            result.Add(cust);
                            continue;
                        }
                    }
                    catch { }

                    result.Add(new CustomerDM
                    {
                        MasterAccountID = e.MasterAccountId,
                        AccountName = e.AccountName,
                        Phone = e.Phone,
                        Email = e.Email,
                        NRIC = e.NRIC,
                        MembershipTypeName = e.MembershipTypeName
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CustomerService] SQLite customer search error: {ex.Message}");
                return new List<CustomerDM>();
            }
        }

        private async Task<CustomerDM?> GetSingleCustomerFromSqliteAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return null;

            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);

                var kw = keyword.Trim();
                var entity = await db.LocalCustomers.FirstOrDefaultAsync(c =>
                    c.MasterAccountId == kw ||
                    c.Phone == kw);

                if (entity != null)
                {
                    try
                    {
                        var cust = JsonSerializer.Deserialize<CustomerDM>(entity.RawJson);
                        if (cust != null) return cust;
                    }
                    catch { }

                    return new CustomerDM
                    {
                        MasterAccountID = entity.MasterAccountId,
                        AccountName = entity.AccountName,
                        Phone = entity.Phone,
                        Email = entity.Email,
                        NRIC = entity.NRIC,
                        MembershipTypeName = entity.MembershipTypeName
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CustomerService] SQLite single customer error: {ex.Message}");
            }

            return null;
        }

        private async Task CacheCustomersToSqliteAsync(List<CustomerDM> customers)
        {
            if (customers == null || customers.Count == 0) return;

            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);

                foreach (var cust in customers)
                {
                    if (string.IsNullOrWhiteSpace(cust.MasterAccountID)) continue;

                    var existing = await db.LocalCustomers.FirstOrDefaultAsync(x => x.MasterAccountId == cust.MasterAccountID);
                    var json = JsonSerializer.Serialize(cust);

                    if (existing != null)
                    {
                        existing.AccountName = cust.AccountName ?? "";
                        existing.Phone = cust.Phone ?? "";
                        existing.Email = cust.Email ?? "";
                        existing.NRIC = cust.NRIC ?? "";
                        existing.MembershipTypeName = cust.MembershipTypeName ?? "";
                        existing.RawJson = json;
                        existing.LastUpdatedAtUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        db.LocalCustomers.Add(new LocalCustomerEntity
                        {
                            MasterAccountId = cust.MasterAccountID,
                            AccountName = cust.AccountName ?? "",
                            Phone = cust.Phone ?? "",
                            Email = cust.Email ?? "",
                            NRIC = cust.NRIC ?? "",
                            MembershipTypeName = cust.MembershipTypeName ?? "",
                            RawJson = json,
                            LastUpdatedAtUtc = DateTime.UtcNow
                        });
                    }
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CustomerService] Error caching customers to SQLite: {ex.Message}");
            }
        }

        public async Task<ApiResponseRoot<CustomerCreateSuccess>?> CreateCustomer(CustomerDM customer)
        {
            var response = await _customerAC.CreateCustomerAsync(customer);
            if (response?.statusCode == 200)
            {
                _ = CacheCustomersToSqliteAsync(new List<CustomerDM> { customer });
            }
            return response;
        }

        public async Task<ApiResponseRoot<string>?> UpdateCustomer(CustomerDM customer)
        {
            var response = await _customerAC.UpdateCustomerAsync(customer);
            if (response?.statusCode == 200)
            {
                _ = CacheCustomersToSqliteAsync(new List<CustomerDM> { customer });
            }
            return response;
        }

        public async Task<ApiResponseRoot<CreditResponseDTO>> GetCreditBalance(string id)
        {
            return await _customerAC.FetchCreditBalance(id);
        }

        public async Task<MemberBalanceSummaryResult?> GetMemberBalanceSummaryAsync(string customerId)
        {
            var request = new MemberBalanceSummaryRequest { id = customerId };
            var response = await _customerAC.GetMemberBalanceSummary(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return null;
        }

        public async Task<List<MemberOtherBalanceSummaryResult>?> GetMemberOtherBalanceSummaryAsync(string customerId, DateTime cutOffDate)
        {
            var request = new MemberOtherBalanceSummaryRequest
            {
                id = customerId,
                cutOffDate = cutOffDate
            };

            var response = await _customerAC.GetMemberOtherBalanceSummary(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return null;
        }

        public async Task<ApiResponseRoot<Dictionary<string, List<CustomerServiceRecordsDM>>>?> GetCustomerServiceRecordByMonthAsync(string customerId, int year, int month)
        {
            var request = new CustomerServiceRecordRequest
            {
                customerId = customerId,
                year = year,
                month = month
            };
            return await _customerAC.GetCustomerServiceRecordByMonth(request);
        }
    }
}
