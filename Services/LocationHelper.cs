using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models.Entities;

namespace Project.Services
{
    public class LocationImportResult
    {
        public int RowsRead { get; set; }
        public int ProvincesAdded { get; set; }
        public int TownsAdded { get; set; }
        public int SuburbsAdded { get; set; }
        public int SuburbsSkipped { get; set; }
        public int UnknownProvinceRows { get; set; }
        public bool Replaced { get; set; }
    }

    public static class LocationHelper
    {
        // Looks a suburb up and returns the authoritative names + postal code.
        public static (int SuburbId, string Suburb, string Town, string PostalCode)? Resolve(ApplicationDbContext db, int suburbId)
        {
            try
            {
                var x = db.Suburbs.AsNoTracking().Where(s => s.SuburbId == suburbId)
                    .Select(s => new { s.SuburbId, s.Name, Town = s.Town.Name, s.PostalCode }).FirstOrDefault();
                return x == null ? null : (x.SuburbId, x.Name, x.Town, x.PostalCode.Trim());
            }
            catch { return null; } // location tables missing -> caller falls back to the typed text
        }

        // Province / town / suburb / postal code behind a saved SuburbId (empty values if none).
        public static (int? ProvinceId, string Town, string Suburb, string PostalCode) Describe(ApplicationDbContext db, int? suburbId)
        {
            if (suburbId == null || suburbId <= 0) return (null, "", "", "");
            try
            {
                var x = db.Suburbs.AsNoTracking().Where(s => s.SuburbId == suburbId)
                    .Select(s => new { s.Name, Town = s.Town.Name, Province = (int)s.Town.ProvinceId, s.PostalCode }).FirstOrDefault();
                return x == null ? (null, "", "", "") : (x.Province, x.Town, x.Name, x.PostalCode.Trim());
            }
            catch { return (null, "", "", ""); }
        }

        // Turns what the address form posted into a real SuburbId.
        //  - picked from the dropdowns: the SuburbId is used as is;
        //  - "not listed" (typed in): the town and suburb are added to the tables under the chosen province,
        //    so every address always lives in the normalised tables.
        // Returns null + an error message when the posted values are not usable.
        public static int? ResolveOrCreate(ApplicationDbContext db, int? suburbId, int? provinceId, string? town, string? suburb, string? postal, out string? error)
        {
            error = null;
            if (suburbId is int sid && sid > 0)
            {
                if (db.Suburbs.Any(s => s.SuburbId == sid)) return sid;
                error = "Please choose your suburb from the list.";
                return null;
            }
            town = (town ?? "").Trim(); suburb = (suburb ?? "").Trim(); postal = (postal ?? "").Trim();
            if (provinceId == null || provinceId <= 0 || town.Length == 0 || suburb.Length == 0)
            { error = "Please choose your province, town and suburb."; return null; }
            if (postal.Length != 4 || !postal.All(char.IsDigit))
            { error = "Postal code must be 4 digits."; return null; }
            if (town.Length > 100 || suburb.Length > 100) { error = "Town or suburb name is too long."; return null; }

            var pid = (byte)provinceId.Value;
            if (!db.Provinces.Any(p => p.ProvinceId == pid)) { error = "Please choose a valid province."; return null; }

            var tn = LocationParser.Norm(town);
            var t = db.Towns.Where(x => x.ProvinceId == pid).AsEnumerable().FirstOrDefault(x => LocationParser.Norm(x.Name) == tn);
            if (t == null) { t = new Town { Name = town, ProvinceId = pid }; db.Towns.Add(t); db.SaveChanges(); }

            var sn = LocationParser.Norm(suburb);
            var sub = db.Suburbs.Where(x => x.TownId == t.TownId).AsEnumerable().FirstOrDefault(x => LocationParser.Norm(x.Name) == sn);
            if (sub == null) { sub = new Suburb { Name = suburb, TownId = t.TownId, PostalCode = postal }; db.Suburbs.Add(sub); db.SaveChanges(); }
            return sub.SuburbId;
        }

        public static LocationImportResult Import(ApplicationDbContext db, List<LocationRow> rows, bool replaceExisting = false)
        {
            var result = new LocationImportResult { RowsRead = rows.Count };

            if (replaceExisting)
            {
                // Clear the old list, but never a suburb that a patient/admin address still points to.
                db.Database.ExecuteSqlRaw(@"
                    DELETE FROM Suburb WHERE SuburbId NOT IN (
                        SELECT SuburbId FROM PatientProfile WHERE SuburbId IS NOT NULL
                        UNION SELECT SuburbId FROM AdminProfile WHERE SuburbId IS NOT NULL);
                    DELETE FROM Town WHERE TownId NOT IN (SELECT TownId FROM Suburb);");
                db.ChangeTracker.Clear();
                result.Replaced = true;
            }
            rows = rows.Concat(LocationParser.BuiltInSuburbs()).ToList();

            // Provinces: reuse the 9 seeded ones (match ignoring case, spaces and hyphens).
            var provinces = db.Provinces.ToList();
            if (provinces.Count == 0)
            {
                var names = new[] { "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape" };
                byte id = 1;
                foreach (var n in names) { var p = new Province { ProvinceId = id++, Name = n }; db.Provinces.Add(p); provinces.Add(p); result.ProvincesAdded++; }
                db.SaveChanges();
            }
            var provByNorm = provinces.ToDictionary(p => LocationParser.Norm(p.Name), p => p);

            var towns = db.Towns.ToList()
                .GroupBy(t => (t.ProvinceId, LocationParser.Norm(t.Name))).ToDictionary(g => g.Key, g => g.First());
            var suburbKeys = db.Suburbs.AsNoTracking().Select(s => new { s.TownId, s.Name }).ToList()
                .Select(s => (s.TownId, LocationParser.Norm(s.Name))).ToHashSet();

            db.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                // 1) towns
                foreach (var r in rows)
                {
                    if (!provByNorm.TryGetValue(LocationParser.Norm(r.Province), out var prov)) continue;
                    var key = (prov.ProvinceId, LocationParser.Norm(r.Town));
                    if (!towns.ContainsKey(key))
                    {
                        var t = new Town { Name = r.Town, ProvinceId = prov.ProvinceId };
                        db.Towns.Add(t); towns[key] = t; result.TownsAdded++;
                    }
                }
                db.SaveChanges();

                // 2) suburbs
                int pending = 0;
                foreach (var r in rows)
                {
                    if (!provByNorm.TryGetValue(LocationParser.Norm(r.Province), out var prov)) { result.UnknownProvinceRows++; continue; }
                    var town = towns[(prov.ProvinceId, LocationParser.Norm(r.Town))];
                    var skey = (town.TownId, LocationParser.Norm(r.Suburb));
                    if (!suburbKeys.Add(skey)) { result.SuburbsSkipped++; continue; }
                    db.Suburbs.Add(new Suburb { Name = r.Suburb, TownId = town.TownId, PostalCode = r.PostalCode });
                    result.SuburbsAdded++;
                    if (++pending >= 500) { db.SaveChanges(); pending = 0; }
                }
                db.SaveChanges();
            }
            finally { db.ChangeTracker.AutoDetectChangesEnabled = true; }
            return result;
        }
    }
}
