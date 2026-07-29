// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;

namespace BBSApp.Services;

public sealed class Site
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Manager { get; set; } = "";
    public string Status { get; set; } = "Active";

    public JsonObject ToJson() => new()
    { ["id"] = Id, ["name"] = Name, ["location"] = Location, ["manager"] = Manager, ["status"] = Status };
    public static Site FromJson(JsonObject o) => new()
    {
        Id = o["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
        Name = o["name"]?.GetValue<string>() ?? "",
        Location = o["location"]?.GetValue<string>() ?? "",
        Manager = o["manager"]?.GetValue<string>() ?? "",
        Status = o["status"]?.GetValue<string>() ?? "Active"
    };
}

/// <summary>A labour / plant / material resource with a rate.</summary>
public sealed class Resource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "Labour";   // Labour | Plant | Material
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "day";
    public double Rate { get; set; }

    public static readonly string[] Kinds = { "Labour", "Plant", "Material" };

    public JsonObject ToJson() => new()
    { ["id"] = Id, ["kind"] = Kind, ["name"] = Name, ["unit"] = Unit, ["rate"] = Rate };
    public static Resource FromJson(JsonObject o) => new()
    {
        Id = o["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
        Kind = o["kind"]?.GetValue<string>() ?? "Labour",
        Name = o["name"]?.GetValue<string>() ?? "",
        Unit = o["unit"]?.GetValue<string>() ?? "day",
        Rate = o["rate"]?.GetValue<double>() ?? 0
    };
}

public sealed class Employee
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Designation { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string WageType { get; set; } = "Monthly";  // Monthly | Daily
    public double Rate { get; set; }                    // monthly salary or daily wage
    public string Phone { get; set; } = "";
    public bool Active { get; set; } = true;

    public static readonly string[] WageTypes = { "Monthly", "Daily" };

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["code"] = Code, ["name"] = Name, ["designation"] = Designation,
        ["site_id"] = SiteId, ["wage_type"] = WageType, ["rate"] = Rate,
        ["phone"] = Phone, ["active"] = Active ? 1 : 0
    };
    public static Employee FromJson(JsonObject o) => new()
    {
        Id = o["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
        Code = o["code"]?.GetValue<string>() ?? "",
        Name = o["name"]?.GetValue<string>() ?? "",
        Designation = o["designation"]?.GetValue<string>() ?? "",
        SiteId = o["site_id"]?.GetValue<string>() ?? "",
        WageType = o["wage_type"]?.GetValue<string>() ?? "Monthly",
        Rate = o["rate"]?.GetValue<double>() ?? 0,
        Phone = o["phone"]?.GetValue<string>() ?? "",
        Active = (o["active"]?.GetValue<int>() ?? 1) != 0
    };
}

/// <summary>Attendance + advance for one employee in one month (yyyy-MM).</summary>
public sealed class PayrollRecord
{
    public string EmployeeId { get; set; } = "";
    public string Month { get; set; } = "";
    public double DaysPresent { get; set; }
    public double Advance { get; set; }

    public JsonObject ToJson() => new()
    { ["employee_id"] = EmployeeId, ["month"] = Month, ["days_present"] = DaysPresent, ["advance"] = Advance };
    public static PayrollRecord FromJson(JsonObject o) => new()
    {
        EmployeeId = o["employee_id"]?.GetValue<string>() ?? "",
        Month = o["month"]?.GetValue<string>() ?? "",
        DaysPresent = o["days_present"]?.GetValue<double>() ?? 0,
        Advance = o["advance"]?.GetValue<double>() ?? 0
    };
}

/// <summary>Sites, resources, employees, and monthly attendance/payroll.</summary>
public sealed class OrgBook
{
    public ObservableCollection<Site> Sites { get; } = new();
    public ObservableCollection<Resource> Resources { get; } = new();
    public ObservableCollection<Employee> Employees { get; } = new();
    public List<PayrollRecord> Payroll { get; } = new();
    public double WorkingDays { get; set; } = 26;

    public string SiteName(string id) => Sites.FirstOrDefault(s => s.Id == id)?.Name ?? "(unassigned)";

    public PayrollRecord GetPayroll(string employeeId, string month)
    {
        var rec = Payroll.FirstOrDefault(p => p.EmployeeId == employeeId && p.Month == month);
        if (rec is null)
        {
            rec = new PayrollRecord { EmployeeId = employeeId, Month = month };
            Payroll.Add(rec);
        }
        return rec;
    }

    /// <summary>Gross pay for an employee given days present in the month.</summary>
    public double Gross(Employee e, double daysPresent)
    {
        if (e.WageType.Equals("Daily", StringComparison.OrdinalIgnoreCase))
            return e.Rate * daysPresent;
        double wd = WorkingDays <= 0 ? 26 : WorkingDays;
        return e.Rate * (daysPresent / wd);
    }

    public void EnsureSeeded()
    {
        if (Resources.Count == 0)
        {
            void R(string kind, string name, string unit, double rate) =>
                Resources.Add(new Resource { Kind = kind, Name = name, Unit = unit, Rate = rate });
            R("Labour", "Mason (1st class)", "day", 900);
            R("Labour", "Helper / beldar", "day", 600);
            R("Labour", "Bar bender", "day", 850);
            R("Labour", "Carpenter (shuttering)", "day", 900);
            R("Plant", "Concrete mixer (10/7)", "day", 1200);
            R("Plant", "Needle vibrator", "day", 500);
            R("Plant", "JCB / excavator", "hour", 1100);
            R("Material", "Cement (OPC 53)", "bag", 400);
            R("Material", "River sand", "cft", 60);
            R("Material", "20 mm aggregate", "cft", 55);
        }
    }

    public void Clear()
    {
        Sites.Clear(); Resources.Clear(); Employees.Clear(); Payroll.Clear();
        WorkingDays = 26;
    }

    public JsonObject ToJson()
    {
        JsonArray Arr<T>(IEnumerable<T> src, Func<T, JsonObject> f) { var a = new JsonArray(); foreach (var x in src) a.Add(f(x)); return a; }
        return new JsonObject
        {
            ["working_days"] = WorkingDays,
            ["sites"] = Arr(Sites, s => s.ToJson()),
            ["resources"] = Arr(Resources, r => r.ToJson()),
            ["employees"] = Arr(Employees, e => e.ToJson()),
            ["payroll"] = Arr(Payroll, p => p.ToJson())
        };
    }

    public void LoadFrom(JsonObject? o)
    {
        Clear();
        if (o is null) { EnsureSeeded(); return; }
        WorkingDays = o["working_days"]?.GetValue<double>() ?? 26;
        if (o["sites"] is JsonArray sa) foreach (var it in sa) if (it is JsonObject so) Sites.Add(Site.FromJson(so));
        if (o["resources"] is JsonArray ra) foreach (var it in ra) if (it is JsonObject ro) Resources.Add(Resource.FromJson(ro));
        if (o["employees"] is JsonArray ea) foreach (var it in ea) if (it is JsonObject eo) Employees.Add(Employee.FromJson(eo));
        if (o["payroll"] is JsonArray pa) foreach (var it in pa) if (it is JsonObject po) Payroll.Add(PayrollRecord.FromJson(po));
        EnsureSeeded();
    }
}
