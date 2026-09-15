using System.Collections.Generic;
using System.Linq;

namespace SenangRetails.Shared.Services
{
    public class WorkspaceService
    {
        public event Action? OnChanged;

        public bool IsLoaded { get; private set; } = false;

        public List<ModuleCategory> ModuleCategories { get; private set; } = new();

        public WorkspaceService()
        {
            InitDefaults();
        }

        private void InitDefaults()
        {
            ModuleCategories = new List<ModuleCategory>
            {
                new ModuleCategory
                {
                    Name = "MAIN",
                    Modules = new List<WorkspaceModule>
                    {
                        new WorkspaceModule { Name = "Member", Icon = "fa-solid fa-users", Route = "members", Location = "Home" },
                        new WorkspaceModule { Name = "Orders", Icon = "fa-solid fa-shopping-bag", Route = "orders", Location = "Home" },
                        new WorkspaceModule {Name = "Sales History", Icon = "fas fa-history", Route = "sales-history", Location = "Home"}
                    }
                },
                new ModuleCategory
                {
                    Name = "STAFF",
                    Modules = new List<WorkspaceModule>
                    {
                        new WorkspaceModule { Name = "Staff", Icon = "fa-solid fa-users", Route = "staff", Location = "Home" }
                    }
                },
                new ModuleCategory
                {
                    Name = "FINANCERECORD",
                    Modules = new List<WorkspaceModule>
                    {
                        new WorkspaceModule { Name = "Report", Icon = "fa-solid fa-chart-line", Route = "report", Location = "Home" },
                        new WorkspaceModule { Name = "EInvoice", Icon = "fa-solid fa-file-invoice", Route = "EInvoices", Location = "Admin" },
                    }
                },
                new ModuleCategory
                {
                    Name = "OPERATIONINVENTORY",
                    Modules = new List<WorkspaceModule>
                    {
                        new WorkspaceModule { Name = "Menu", Icon = "fa-solid fa-bars", Route = "menu", Location = "Admin" },
                    }
                },
                new ModuleCategory
                {
                    Name = "ADMIN",
                    Modules = new List<WorkspaceModule>
                    {
                        new WorkspaceModule { Name = "Setting", Icon = "fa-solid fa-gear", Route = "setting", Location = "Admin" },
                        new WorkspaceModule { Name = "User Control", Icon = "fa-solid fa-user-gear", Route = "permissionsetting", Location = "Admin" },
                    }
                }
            };
        }

        public void SetModuleLocation(WorkspaceModule module, string location)
        {
            module.Location = location;
            OnChanged?.Invoke();
        }

        public bool IsModuleInLocation(string moduleName, string location)
        {
            foreach (var category in ModuleCategories)
            {
                var module = category.Modules.FirstOrDefault(m => m.Name == moduleName);
                if (module != null) return module.Location == location;
            }
            return false;
        }

        public List<WorkspaceModule> GetModulesForLocation(string location)
        {
            return ModuleCategories
                .SelectMany(c => c.Modules)
                .Where(m => m.Location == location)
                .ToList();
        }

        // Serialization format: "Name1|Location1,Name2|Location2,..."
        public string ToJson()
        {
            var pairs = ModuleCategories
                .SelectMany(c => c.Modules)
                .Select(m => m.Name + "|" + m.Location);
            return string.Join(",", pairs);
        }

        public void LoadFromJson(string data)
        {
            try
            {
                foreach (var pair in data.Split(','))
                {
                    var kv = pair.Split('|');
                    if (kv.Length == 2)
                    {
                        var name = kv[0];
                        var loc = kv[1];
                        foreach (var category in ModuleCategories)
                            foreach (var module in category.Modules)
                                if (module.Name == name) module.Location = loc;
                    }
                }
            }
            catch { }
            IsLoaded = true;
        }

        public void MarkLoaded() => IsLoaded = true;

    }

    public class WorkspaceModule
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Route { get; set; } = "";
        public string Location { get; set; } = "Disabled";
    }

    public class ModuleCategory
    {
        public string Name { get; set; } = "";
        public List<WorkspaceModule> Modules { get; set; } = new();
    }
}
