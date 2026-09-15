using Beauty_Aesthetics_WebPos.Components.Models;

namespace Beauty_Aesthetics_WebPos.Components.Services
{
    public class SalesService
    {
        private List<Transaction> _transactions = new();
        private List<Branch> _branches = new();
        private int _nextTransactionId = 1;

        public SalesService()
        {
            InitializeBranches();
            InitializeSampleData();
        }

        #region Branch Management

        private void InitializeBranches()
        {
            _branches = new List<Branch>
            {
                new() { Id = 1, Name = "Main Branch - KL", Code = "KL01", Location = "Kuala Lumpur", ContactNumber = "+60312345678", IsActive = true },
                new() { Id = 2, Name = "Subang Jaya", Code = "SJ01", Location = "Selangor", ContactNumber = "+60356781234", IsActive = true },
                new() { Id = 3, Name = "Penang", Code = "PG01", Location = "Penang", ContactNumber = "+60442345678", IsActive = true },
                new() { Id = 4, Name = "Johor Bahru", Code = "JB01", Location = "Johor", ContactNumber = "+60723456789", IsActive = true },
                new() { Id = 5, Name = "Ipoh", Code = "IP01", Location = "Perak", ContactNumber = "+60534567890", IsActive = true }
            };
        }

        public List<Branch> GetBranches()
        {
            return _branches.Where(b => b.IsActive).ToList();
        }

        public Branch? GetBranchByName(string name)
        {
            return _branches.FirstOrDefault(b => b.Name == name);
        }

        #endregion

        #region Transaction Management

        private void InitializeSampleData()
        {
            _transactions = new List<Transaction>
            {
                new() {
                    Id = _nextTransactionId++,
                    Date = DateTime.Today.AddDays(-1),
                    InvoiceNumber = "INV-2024-001",
                    CustomerName = "John Doe",
                    CustomerContact = "+60123456789",
                    CustomerEmail = "john.doe@email.com",
                    Branch = "Main Branch - KL",
                    Type = "Service",
                    ItemCount = 3,
                    Amount = 450.00m,
                    Subtotal = 424.53m,
                    Tax = 25.47m,
                    Discount = 0m,
                    PaymentMethod = "Cash",
                    Status = "Paid",
                    ReferenceNumber = "",
                    Notes = "Customer requested extra care for sensitive skin.",
                    Items = new List<TransactionItem>
                    {
                        new() { Id = 1, Name = "Facial Treatment", Category = "Service", Quantity = 1, UnitPrice = 200.00m, TotalPrice = 200.00m },
                        new() { Id = 2, Name = "Hair Treatment", Category = "Service", Quantity = 1, UnitPrice = 150.00m, TotalPrice = 150.00m },
                        new() { Id = 3, Name = "Manicure", Category = "Service", Quantity = 1, UnitPrice = 74.53m, TotalPrice = 74.53m }
                    },
                    CreatedBy = "Admin"
                },
                new() {
                    Id = _nextTransactionId++,
                    Date = DateTime.Today.AddDays(-1),
                    InvoiceNumber = "INV-2024-002",
                    CustomerName = "Jane Smith",
                    CustomerContact = "+60198765432",
                    CustomerEmail = "jane.smith@email.com",
                    Branch = "Subang Jaya",
                    Type = "Product",
                    ItemCount = 2,
                    Amount = 150.00m,
                    Subtotal = 141.51m,
                    Tax = 8.49m,
                    Discount = 0m,
                    PaymentMethod = "Card",
                    Status = "Paid",
                    ReferenceNumber = "CARD-20241121-001",
                    Notes = "",
                    Items = new List<TransactionItem>
                    {
                        new() { Id = 1, Name = "Face Serum", Category = "Product", Quantity = 1, UnitPrice = 89.90m, TotalPrice = 89.90m },
                        new() { Id = 2, Name = "Moisturizer", Category = "Product", Quantity = 1, UnitPrice = 51.61m, TotalPrice = 51.61m }
                    },
                    CreatedBy = "Admin"
                },
                new() {
                    Id = _nextTransactionId++,
                    Date = DateTime.Today.AddDays(-2),
                    InvoiceNumber = "INV-2024-003",
                    CustomerName = "Bob Johnson",
                    CustomerContact = "+60187654321",
                    CustomerEmail = "bob.johnson@email.com",
                    Branch = "Penang",
                    Type = "Service",
                    ItemCount = 2,
                    Amount = 320.00m,
                    Subtotal = 301.89m,
                    Tax = 18.11m,
                    Discount = 0m,
                    PaymentMethod = "Online Transfer",
                    Status = "Pending",
                    ReferenceNumber = "TRF-20241120-045",
                    Notes = "Payment pending verification.",
                    Items = new List<TransactionItem>
                    {
                        new() { Id = 1, Name = "Body Massage", Category = "Service", Quantity = 1, UnitPrice = 180.00m, TotalPrice = 180.00m },
                        new() { Id = 2, Name = "Foot Spa", Category = "Service", Quantity = 1, UnitPrice = 121.89m, TotalPrice = 121.89m }
                    },
                    CreatedBy = "Admin"
                },
                new() {
                    Id = _nextTransactionId++,
                    Date = DateTime.Today.AddDays(-2),
                    InvoiceNumber = "INV-2024-004",
                    CustomerName = "Alice Wong",
                    CustomerContact = "+60123334444",
                    CustomerEmail = "alice.wong@email.com",
                    Branch = "Main Branch - KL",
                    Type = "Service",
                    ItemCount = 5,
                    Amount = 780.00m,
                    Subtotal = 735.85m,
                    Tax = 44.15m,
                    Discount = 0m,
                    PaymentMethod = "Cash",
                    Status = "Paid",
                    ReferenceNumber = "",
                    Notes = "VIP customer - complimentary upgrade applied.",
                    Items = new List<TransactionItem>
                    {
                        new() { Id = 1, Name = "Full Body Treatment", Category = "Service", Quantity = 1, UnitPrice = 400.00m, TotalPrice = 400.00m },
                        new() { Id = 2, Name = "Hair Styling", Category = "Service", Quantity = 1, UnitPrice = 120.00m, TotalPrice = 120.00m },
                        new() { Id = 3, Name = "Nail Art", Category = "Service", Quantity = 1, UnitPrice = 95.00m, TotalPrice = 95.00m },
                        new() { Id = 4, Name = "Eyelash Extension", Category = "Service", Quantity = 1, UnitPrice = 85.85m, TotalPrice = 85.85m },
                        new() { Id = 5, Name = "Makeup", Category = "Service", Quantity = 1, UnitPrice = 35.00m, TotalPrice = 35.00m }
                    },
                    CreatedBy = "Admin"
                },
                new() {
                    Id = _nextTransactionId++,
                    Date = DateTime.Today.AddDays(-3),
                    InvoiceNumber = "INV-2024-005",
                    CustomerName = "Charlie Lee",
                    CustomerContact = "+60165554444",
                    CustomerEmail = "charlie.lee@email.com",
                    Branch = "Johor Bahru",
                    Type = "Product",
                    Amount = 95.00m,
                    Subtotal = 89.62m,
                    Tax = 5.38m,
                    Discount = 0m,
                    ItemCount = 2,
                    PaymentMethod = "E-Wallet",
                    Status = "Paid",
                    ReferenceNumber = "EWALLET-20241119-123",
                    Notes = "",
                    Items = new List<TransactionItem>
                    {
                        new() { Id = 1, Name = "Hair Shampoo", Category = "Product", Quantity = 1, UnitPrice = 45.00m, TotalPrice = 45.00m },
                        new() { Id = 2, Name = "Conditioner", Category = "Product", Quantity = 1, UnitPrice = 44.62m, TotalPrice = 44.62m }
                    },
                    CreatedBy = "Admin"
                },
                new() {
                    Id = _nextTransactionId++,
                    Date = DateTime.Today.AddDays(-3),
                    InvoiceNumber = "INV-2024-006",
                    CustomerName = "Diana Tan",
                    CustomerContact = "+60178889999",
                    CustomerEmail = "diana.tan@email.com",
                    Branch = "Ipoh",
                    Type = "Service",
                    ItemCount = 3,
                    Amount = 550.00m,
                    Subtotal = 518.87m,
                    Tax = 31.13m,
                    Discount = 0m,
                    PaymentMethod = "Card",
                    Status = "Paid",
                    ReferenceNumber = "CARD-20241119-078",
                    Notes = "Regular customer - loyalty points applied.",
                    Items = new List<TransactionItem>
                    {
                        new() { Id = 1, Name = "Facial Deep Cleanse", Category = "Service", Quantity = 1, UnitPrice = 220.00m, TotalPrice = 220.00m },
                        new() { Id = 2, Name = "Whitening Treatment", Category = "Service", Quantity = 1, UnitPrice = 198.87m, TotalPrice = 198.87m },
                        new() { Id = 3, Name = "Facial Mask", Category = "Service", Quantity = 1, UnitPrice = 100.00m, TotalPrice = 100.00m }
                    },
                    CreatedBy = "Admin"
                }
            };
        }

        public List<Transaction> GetAllTransactions()
        {
            return _transactions.OrderByDescending(t => t.Date).ToList();
        }

        public List<Transaction> GetTransactionsByFilter(SalesFilter filter)
        {
            var query = _transactions.Where(t => !t.IsDeleted).AsQueryable(); 

            if (filter.StartDate.HasValue)
                query = query.Where(t => t.Date >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(t => t.Date <= filter.EndDate.Value);

            if (!string.IsNullOrEmpty(filter.Branch))
                query = query.Where(t => t.Branch == filter.Branch);

            if (!string.IsNullOrEmpty(filter.Status))
                query = query.Where(t => t.Status == filter.Status);

            if (!string.IsNullOrEmpty(filter.PaymentMethod))
                query = query.Where(t => t.PaymentMethod == filter.PaymentMethod);

            if (!string.IsNullOrEmpty(filter.Type))
                query = query.Where(t => t.Type == filter.Type);

            if (filter.MinAmount.HasValue)
                query = query.Where(t => t.Amount >= filter.MinAmount.Value);

            if (filter.MaxAmount.HasValue)
                query = query.Where(t => t.Amount <= filter.MaxAmount.Value);

            return query.OrderByDescending(t => t.Date).ToList();
        }

        public Transaction? GetTransactionById(int id)
        {
            return _transactions.FirstOrDefault(t => t.Id == id);
        }

        public Transaction? GetTransactionByInvoiceNumber(string invoiceNumber)
        {
            return _transactions.FirstOrDefault(t => t.InvoiceNumber == invoiceNumber);
        }

        public void AddTransaction(Transaction transaction)
        {
            transaction.Id = _nextTransactionId++;
            transaction.CreatedAt = DateTime.Now;
            _transactions.Add(transaction);
        }

        public void UpdateTransaction(Transaction transaction)
        {
            var existing = _transactions.FirstOrDefault(t => t.Id == transaction.Id);
            if (existing != null)
            {
                var index = _transactions.IndexOf(existing);
                transaction.ModifiedAt = DateTime.Now;
                transaction.ModifiedBy = "Admin"; // Set to current user
                _transactions[index] = transaction;
            }
        }

        public void DeleteTransaction(int id)
        {
            // This is now a soft delete - we keep the record for accounting
            CancelTransaction(id, "Deleted by user");
        }

        public void CancelTransaction(int id, string reason, string cancelledBy = "Admin")
        {
            var transaction = _transactions.FirstOrDefault(t => t.Id == id);
            if (transaction != null)
            {
                transaction.Status = "Cancelled";
                transaction.CancelledAt = DateTime.Now;
                transaction.CancelledBy = cancelledBy;
                transaction.CancellationReason = reason;
                transaction.IsDeleted = true; // Soft delete - keeps in database but excluded from reports
            }
        }

        public void VoidTransaction(int id, string reason, string voidedBy = "Admin")
        {
            var transaction = _transactions.FirstOrDefault(t => t.Id == id);
            if (transaction != null)
            {
                transaction.Status = "Voided";
                transaction.CancelledAt = DateTime.Now;
                transaction.CancelledBy = voidedBy;
                transaction.CancellationReason = reason;
                transaction.IsDeleted = true;
            }
        }

        public List<Transaction> GetCancelledTransactions()
        {
            return _transactions.Where(t => t.IsDeleted).OrderByDescending(t => t.CancelledAt).ToList();
        }

        #endregion

        #region Summary & Analytics

        public SalesSummary GetSalesSummary(DateTime? startDate = null, DateTime? endDate = null, string? branch = null)
        {
            var filteredTransactions = _transactions.Where(t => !t.IsDeleted).AsQueryable();

            if (startDate.HasValue)
                filteredTransactions = filteredTransactions.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                filteredTransactions = filteredTransactions.Where(t => t.Date <= endDate.Value);

            if (!string.IsNullOrEmpty(branch))
                filteredTransactions = filteredTransactions.Where(t => t.Branch == branch);

            var transactions = filteredTransactions.ToList();
            var paidTransactions = transactions.Where(t => t.Status == "Paid").ToList();

            var servicesAmount = transactions.Where(t => t.Type == "Service").Sum(t => t.Amount);
            var productsAmount = transactions.Where(t => t.Type == "Product").Sum(t => t.Amount);
            var totalAmount = servicesAmount + productsAmount;

            return new SalesSummary
            {
                TotalSales = totalAmount,
                TotalCash = paidTransactions.Where(t => t.PaymentMethod == "Cash").Sum(t => t.Amount),
                TotalCard = paidTransactions.Where(t => t.PaymentMethod == "Card").Sum(t => t.Amount),
                TotalOnlineTransfer = paidTransactions.Where(t => t.PaymentMethod == "Online Transfer").Sum(t => t.Amount),
                TotalEWallet = paidTransactions.Where(t => t.PaymentMethod == "E-Wallet").Sum(t => t.Amount),
                Outstanding = transactions.Where(t => t.Status == "Pending").Sum(t => t.Amount),
                Rounding = 0.50m, // Sample rounding value
                TotalTransactions = transactions.Count,
                AverageSale = transactions.Any() ? transactions.Average(t => t.Amount) : 0,
                ServicesAmount = servicesAmount,
                ProductsAmount = productsAmount,
                ServicesPercentage = totalAmount > 0 ? ((servicesAmount / totalAmount) * 100).ToString("F2") : "0",
                ProductsPercentage = totalAmount > 0 ? ((productsAmount / totalAmount) * 100).ToString("F2") : "0",
                FromDate = startDate ?? DateTime.Today.AddMonths(-1),
                ToDate = endDate ?? DateTime.Today
            };
        }

        public List<TransactionCategory> GetTransactionCategories(DateTime? startDate = null, DateTime? endDate = null, string? branch = null)
        {
            var filteredTransactions = _transactions.Where(t => !t.IsDeleted).AsQueryable();

            if (startDate.HasValue)
                filteredTransactions = filteredTransactions.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                filteredTransactions = filteredTransactions.Where(t => t.Date <= endDate.Value);

            if (!string.IsNullOrEmpty(branch))
                filteredTransactions = filteredTransactions.Where(t => t.Branch == branch);

            var transactions = filteredTransactions.ToList();
            var total = transactions.Sum(t => t.Amount);

            return new List<TransactionCategory>
            {
                new()
                {
                    Category = "Services",
                    Amount = transactions.Where(t => t.Type == "Service").Sum(t => t.Amount),
                    Count = transactions.Count(t => t.Type == "Service"),
                    Percentage = total > 0 ? (transactions.Where(t => t.Type == "Service").Sum(t => t.Amount) / total) * 100 : 0
                },
                new()
                {
                    Category = "Products",
                    Amount = transactions.Where(t => t.Type == "Product").Sum(t => t.Amount),
                    Count = transactions.Count(t => t.Type == "Product"),
                    Percentage = total > 0 ? (transactions.Where(t => t.Type == "Product").Sum(t => t.Amount) / total) * 100 : 0
                }
            };
        }

        public List<CashFlowItem> GetCashFlowData(DateTime? startDate = null, DateTime? endDate = null, string? branch = null)
        {
            var filteredTransactions = _transactions.Where(t => !t.IsDeleted).AsQueryable();

            if (startDate.HasValue)
                filteredTransactions = filteredTransactions.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                filteredTransactions = filteredTransactions.Where(t => t.Date <= endDate.Value);

            if (!string.IsNullOrEmpty(branch))
                filteredTransactions = filteredTransactions.Where(t => t.Branch == branch);

            var transactions = filteredTransactions.ToList();

            return new List<CashFlowItem>
    {
        new() { Category = "Cash Movement", Amount = transactions.Where(t => t.Status == "Paid").Sum(t => t.Amount), Date = DateTime.Today },
        new() { Category = "Payment Collected", Amount = transactions.Where(t => t.Status == "Paid").Sum(t => t.Amount), Date = DateTime.Today },
        new() { Category = "Outstanding", Amount = transactions.Where(t => t.Status == "Pending").Sum(t => t.Amount), Date = DateTime.Today }
    };
        }

        public List<Transaction> GetActiveTransactions()
        {
            return _transactions.Where(t => !t.IsDeleted).OrderByDescending(t => t.Date).ToList();
        }

        public string GenerateInvoiceNumber()
        {
            return $"INV-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        #endregion
    }
}