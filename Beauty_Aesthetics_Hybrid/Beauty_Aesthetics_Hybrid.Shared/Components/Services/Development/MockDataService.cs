using System.Text.Json;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services
{
    public class MockDataService
    {
        private readonly string basePath;
        private readonly string customersFile;
        private readonly string leadsFile;
        private readonly string reviewsFile;
        private readonly string exportsDir;
        private readonly Random rnd = new();
        private List<Customer> customers = new();
        private List<Lead> leads = new();
        private List<Review> reviews = new();
        private int nextSystemIdNumber = 1;

        private readonly IPathProvider _pathProvider;

        public MockDataService(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;

            string baseDir;
            try
            {
                baseDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                Directory.CreateDirectory(baseDir);
            }
            catch
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeautyAesthetics", "Data");
                Directory.CreateDirectory(baseDir);
            }
            basePath = baseDir;

            exportsDir = Path.Combine(_pathProvider.GetWebRootPath(), "exports");
            Directory.CreateDirectory(exportsDir);

            customersFile = Path.Combine(basePath, "customers.json");
            leadsFile = Path.Combine(basePath, "leads.json");
            reviewsFile = Path.Combine(basePath, "reviews.json");

            if (!File.Exists(customersFile)) File.WriteAllText(customersFile, JsonSerializer.Serialize(new List<Customer>()));
            if (!File.Exists(leadsFile)) File.WriteAllText(leadsFile, JsonSerializer.Serialize(new List<Lead>()));
            if (!File.Exists(reviewsFile)) File.WriteAllText(reviewsFile, JsonSerializer.Serialize(new List<Review>()));

            Load();

            if (!customers.Any())
            {
                customers.Add(new Customer
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Alice",
                    LastName = "Tan",
                    IdentificationNumber = "900101-14-5678",
                    Email = "alice.tan@example.com",
                    DateOfBirth = new DateTime(1990, 1, 1),
                    Gender = "Female",
                    ContactNumber1 = "010-1234567",
                    MembershipType = "VIP",
                    IsVip = true,
                    Country = "Malaysia",
                    State = "Selangor",
                    City = "Petaling Jaya",
                    Postcode = "47301",
                    Occupation = "Manager",
                    IncomeRange = "Above MYR100,000",
                    MaritalStatus = "Married",
                    PreferredLanguage = "English",
                    Referrer = "Jason Lee",
                    Notes = "Allergic to peanuts"
                });

                customers.Add(new Customer
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Ben",
                    LastName = "Lee",
                    IdentificationNumber = "920202-10-1234",
                    Email = "ben.lee@example.com",
                    DateOfBirth = new DateTime(1992, 2, 2),
                    Gender = "Male",
                    ContactNumber1 = "010-2345678",
                    MembershipType = "Standard",
                    IsVip = false,
                    Country = "Malaysia",
                    State = "Kuala Lumpur",
                    City = "Cheras",
                    Postcode = "56100",
                    Occupation = "Technician",
                    IncomeRange = "MYR36,001–MYR60,000",
                    MaritalStatus = "Single",
                    PreferredLanguage = "English"
                });

                SaveCustomers();
            }

            if (!leads.Any())
            {
                leads.Add(new Lead { Id = 1, Name = "Akan", Phone = "010-1001010", Source = "Website", Outlet = "Main", Remarks = "Interested", AttendedBy = "Adrian" });
                leads.Add(new Lead { Id = 2, Name = "Lead 2", Phone = "010-1002020", Source = "Walk-in", Outlet = "Outlet A", Remarks = "Follow up", AttendedBy = "Jason" });
                SaveLeads();
            }

            if (!reviews.Any())
            {
                reviews.Add(new Review { Id = 1, Author = "Alice Johnson", Rating = 5, Text = "Amazing experience! The staff is super friendly and professional.", Source = "google" });
                reviews.Add(new Review { Id = 2, Author = "Bob Smith", Rating = 4, Text = "Very nice atmosphere and good service.", Source = "google" });
                reviews.Add(new Review { Id = 3, Author = "Charlie Brown", Rating = 5, Text = "Best facial treatment I've ever had. Highly recommend!", Source = "internal" });
                reviews.Add(new Review { Id = 4, Author = "Diana Prince", Rating = 3, Text = "The service was okay, but wait time was a bit long.", Source = "internal" });
                SaveReviews();
            }
        }

        void Load()
        {
            customers = JsonSerializer.Deserialize<List<Customer>>(File.ReadAllText(customersFile)) ?? new();
            leads = JsonSerializer.Deserialize<List<Lead>>(File.ReadAllText(leadsFile)) ?? new();
            string rawReviews = File.ReadAllText(reviewsFile);
            reviews = JsonSerializer.Deserialize<List<Review>>(rawReviews) ?? new();

            // Migrate old reviews that don't have a Source property or are legacy reviews
            bool anyMigration = false;
            if (!rawReviews.Contains("\"Source\"") && !rawReviews.Contains("\"source\""))
            {
                foreach (var r in reviews)
                {
                    r.Source = "google"; // Default existing reviews to Google source
                }
                anyMigration = true;
            }
            else
            {
                // Safety check: force legacy reviews by author to google source if saved as internal
                foreach (var r in reviews)
                {
                    if ((r.Author == "Customer A" || r.Author == "test" || r.Author == "Alice Johnson" || r.Author == "Bob Smith") && r.Source != "google")
                    {
                        r.Source = "google";
                        anyMigration = true;
                    }
                }
            }
            if (anyMigration)
            {
                SaveReviews();
            }

            string prefix = "HQCA";
            int maxExisting = 0;

            // ✅ Step 1: Find the highest existing SystemID number
            foreach (var c in customers)
            {
                if (!string.IsNullOrEmpty(c.SystemID) && c.SystemID.StartsWith(prefix))
                {
                    if (int.TryParse(c.SystemID.Substring(prefix.Length), out int n))
                        if (n > maxExisting) maxExisting = n;
                }
            }

            // ✅ Step 2: Assign SystemIDs to customers missing one
            foreach (var c in customers.Where(x => string.IsNullOrEmpty(x.SystemID)))
            {
                maxExisting++;
                c.SystemID = $"{prefix}{maxExisting.ToString("D11")}";
            }

            // ✅ Step 3: Update counter
            nextSystemIdNumber = (customers.Any()) ? maxExisting + 1 : 1;

            // ✅ Step 4: Save if any changes were made
            SaveCustomers();
        }



        void SaveCustomers() => File.WriteAllText(customersFile, JsonSerializer.Serialize(customers, new JsonSerializerOptions { WriteIndented = true }));
        void SaveLeads() => File.WriteAllText(leadsFile, JsonSerializer.Serialize(leads, new JsonSerializerOptions { WriteIndented = true }));
        void SaveReviews() => File.WriteAllText(reviewsFile, JsonSerializer.Serialize(reviews, new JsonSerializerOptions { WriteIndented = true }));

        public DashboardSnapshot GetSnapshot()
        {
            return new DashboardSnapshot
            {
                TotalCustomers = customers.Count + rnd.Next(0, 10),
                ActiveLeads = leads.Count + rnd.Next(0, 5),
                AppointmentsToday = rnd.Next(0, 8),
                Revenue = Math.Round((decimal)(rnd.NextDouble() * 4000 + 500), 2),
                Series = Enumerable.Range(0, 7).Select(i => rnd.Next(50, 200)).ToList(),
                Series2 = Enumerable.Range(0, 7).Select(i => rnd.Next(10, 100)).ToList()
            };
        }

        // Leads
        public List<Lead> GetLeads() => leads.OrderByDescending(l => l.Id).ToList();
        public void AddLead(Lead l) { l.Id = (leads.Any() ? leads.Max(x => x.Id) + 1 : 1); leads.Insert(0, l); SaveLeads(); }
        public void DeleteLead(Lead l) { leads.RemoveAll(x => x.Id == l.Id); SaveLeads(); }

        // FIX: Added the missing UpdateLead method
        public void UpdateLead(Lead l)
        {
            var idx = leads.FindIndex(x => x.Id == l.Id);
            if (idx >= 0)
            {
                leads[idx] = l;
            }
            SaveLeads();
        }

        // Customers
        // Customers
        public List<Customer> GetCustomers() => customers.OrderByDescending(c => c.Id).ToList();

        public void AddCustomer(Customer c)
        {
            c.Id = Guid.NewGuid();

            string prefix = "HQCA";
            c.SystemID = $"{prefix}{nextSystemIdNumber.ToString("D11")}";
            nextSystemIdNumber++;

            customers.Insert(0, c);
            SaveCustomers();
        }


        public void UpdateCustomer(Customer c)
        {
            var idx = customers.FindIndex(x => x.Id == c.Id);
            if (idx >= 0)
            {
                customers[idx] = c;
            }
            else
            {
                c.Id = Guid.NewGuid();
                c.SystemID = $"HQCA{nextSystemIdNumber.ToString("D11")}";
                nextSystemIdNumber++;
                customers.Insert(0, c);
            }


            SaveCustomers();
        }


        public void DeleteCustomer(Customer c)
        {
            customers.RemoveAll(x => x.Id == c.Id);
            SaveCustomers();
        }

        // Reviews
        public List<Review> GetReviews() => reviews.OrderByDescending(r => r.Id).ToList();
        public void UpdateReview(Review r)
        {
            var idx = reviews.FindIndex(x => x.Id == r.Id);
            if (idx >= 0)
            {
                reviews[idx] = r;
            }
            else
            {
                reviews.Insert(0, r);
            }
            SaveReviews();
        }

        // Export
        public string ExportCustomersCsv()
        {
            var csv = new System.Text.StringBuilder();

            if (customers == null) customers = new List<Customer>();

            // ✅ COMPREHENSIVE Header row with ALL fields
            csv.AppendLine("Current System ID,Customer Code,Customer Name,Salutation,First Name,Last Name,Customer Group,Birth Year,Birth Month,Birth Day,Gender,NRIC,Member No,Member Since,Membership Valid From,Membership Valid To,BranchID,RefererID,Race,Religion,Language,Address1,Address2,Address3,Zip Code,City,State,Country,Email,Mobile Phone,Phone 2,Account Since,Alert Message,BRN (TIN),Customer Source,Member Password,Comment,Consultant,External Code,VIP Status,Use Consolidated E-Invoice,Invoice ID Type,Invoice ID Number,Use Existing ID,Smoker,Drug Allergies,Current Illness,Customer Tag,Known Allergies,Department,Employee No,Payee Origin,Occupation,Income Range,Marital Status,Opt-in Notifications,Opt-in Promotions,Photo Path,Relationships,Guardians");

            foreach (var c in customers)
            {
                // Helper: Format relationships/guardians
                var relationshipsStr = c.Relationships != null && c.Relationships.Any()
                    ? string.Join("; ", c.Relationships.Select(r => $"{r.Name} ({r.Relationship}) - {r.Contact}" + (r.IsEmergencyContact ? " [Emergency]" : "")))
                    : "";

                var guardiansStr = c.Guardians != null && c.Guardians.Any()
                    ? string.Join("; ", c.Guardians.Select(g => $"{g.Name} ({g.Relationship}) - {g.Contact}" + (g.IsEmergencyContact ? " [Emergency]" : "")))
                    : "";

                var row = new List<string?>
        {
            // Basic Info
            c.SystemID ?? "",
            "", // Customer Code
            $"{c.FirstName} {c.LastName}",
            c.Salutation ?? "",
            c.FirstName ?? "",
            c.LastName ?? "",
            c.MembershipType ?? "",
            
            // Birth Date
            c.DateOfBirth?.Year.ToString() ?? "",
            c.DateOfBirth?.Month.ToString() ?? "",
            c.DateOfBirth?.Day.ToString() ?? "",
            
            // Personal
            c.Gender ?? "",
            c.IdentificationNumber ?? "",
            "", // Member No
            "", // Member Since
            "", // Membership Valid From
            "", // Membership Valid To
            "", // BranchID
            c.Referrer ?? "",
            c.Race ?? "",
            c.Religion ?? "",
            c.PreferredLanguage ?? "",
            
            // Address
            c.Address1 ?? "",
            c.Address2 ?? "",
            c.Address3 ?? "",
            c.Postcode ?? "",
            c.City ?? "",
            c.State ?? "",
            c.Country ?? "",
            
            // Contact
            c.Email ?? "",
            c.ContactNumber1 ?? "",
            c.ContactNumber2 ?? "",
            "", // Account Since
            c.AlertAllergies ?? "", // Alert Message
            
            // ✅ BRN now contains TIN
            c.TaxIdentificationNumber ?? "", // BRN (TIN)
            c.Source ?? "", // Customer Source
            "", // Member Password
            c.Notes ?? "",
            c.Consultant ?? "",
            c.ExternalCode ?? "",
            
            // VIP & E-Invoice
            c.IsVip ? "Yes" : "No",
            c.UseConsolidatedEInvoice ? "Yes" : "No",
            c.InvoiceIdentificationType ?? "",
            c.InvoiceIdentificationNumber ?? "",
            c.UseExistingIdentification ? "Yes" : "No",
            
            // Medical
            c.Smoker ?? "",
            c.DrugAllergies ?? "",
            c.CurrentIllness ?? "",
            c.CustomerTag ?? "",
            c.AlertAllergies ?? "",
            
            // Employment
            c.Department ?? "",
            c.EmployeeNo ?? "",
            c.PayeeOrigin ?? "",
            
            // Miscellaneous
            c.Occupation ?? "",
            c.IncomeRange ?? "",
            c.MaritalStatus ?? "",
            
            // Marketing
            c.OptInNotifications ? "Yes" : "No",
            c.OptInPromotions ? "Yes" : "No",
            
            // Other
            c.PhotoPath ?? "",
            relationshipsStr,
            guardiansStr
        };

                csv.AppendLine(string.Join(",", row.Select(Escape)));
            }

            var path = Path.Combine(exportsDir, "customers.csv");
            File.WriteAllText(path, csv.ToString());
            return "/exports/customers.csv";
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\""))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }


        // Async wrappers (for Customers.razor compatibility)
        public Task<List<Customer>> GetCustomersAsync() => Task.FromResult(GetCustomers());

        public Task AddCustomerAsync(Customer c)
        {
            AddCustomer(c);
            return Task.CompletedTask;
        }

        public Task UpdateCustomerAsync(Customer c)
        {
            UpdateCustomer(c);
            return Task.CompletedTask;
        }

        public Task DeleteCustomerAsync(Customer c)
        {
            DeleteCustomer(c);
            return Task.CompletedTask;
        }
    } // ← close class MockDataService pro
}