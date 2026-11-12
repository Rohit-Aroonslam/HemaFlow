using System.Globalization;
using System.Text;

namespace Project.Services
{
    // One row of location data: where a suburb sits and its postal code.
    public class LocationRow
    {
        public string Province { get; set; } = "";
        public string Town { get; set; } = "";
        public string Suburb { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }

    // Pure text parsing (no database) so it can be tested on its own.
    // Understands two formats:
    //   1) GeoNames postal-code dump for South Africa (ZA.txt, tab separated) -
    //      free, CC-BY 4.0, from https://download.geonames.org/export/zip/ZA.zip
    //   2) A simple CSV with a header row: Province,Town,Suburb,PostalCode
    public static class LocationParser
    {
        public static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var sb = new StringBuilder();
            foreach (var ch in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private static readonly string[] MunicipalitySuffixes =
        {
            " Metropolitan Municipality", " District Municipality", " Local Municipality", " Municipality", " Metropolitan", " District"
        };

        public static string CleanTownName(string? name)
        {
            var n = (name ?? "").Trim();
            foreach (var suffix in MunicipalitySuffixes)
                if (n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { n = n.Substring(0, n.Length - suffix.Length).Trim(); break; }
            return Aliases.TryGetValue(Norm(n), out var friendly) ? friendly : n;
        }

        // Metro municipality names -> the city names people actually pick (and the towns already seeded by script 012).
        private static readonly Dictionary<string, string> Aliases = new()
        {
            ["nelsonmandelabay"] = "Gqeberha",
            ["buffalocity"] = "East London",
            ["cityofcapetown"] = "Cape Town",
            ["cityofjohannesburg"] = "Johannesburg",
            ["cityoftshwane"] = "Pretoria",
            ["ethekwini"] = "Durban",
            ["mangaung"] = "Bloemfontein",
            ["msunduzi"] = "Pietermaritzburg",
            ["portelizabeth"] = "Gqeberha",
            ["pietersburg"] = "Polokwane",
            ["potgietersrus"] = "Mokopane",
            ["witbank"] = "Emalahleni",
            ["nelspruit"] = "Mbombela",
            ["louistrichardt"] = "Makhado",
            ["grahamstown"] = "Makhanda",
            ["umtata"] = "Mthatha",
            ["queenstown"] = "Komani",
            ["uitenhage"] = "Kariega",
            ["kingwilliamstown"] = "Qonce",
            ["kingwilliamstad"] = "Qonce",
            ["grahamstad"] = "Makhanda",
            ["aliwalnoord"] = "Aliwal North",
            ["jeffreysbaai"] = "Jeffreys Bay",
            ["kaapstad"] = "Cape Town",
            ["ooslonden"] = "East London",
            ["mosselbaai"] = "Mossel Bay",
            ["bisho"] = "Bhisho",
            ["cityofekurhuleni"] = "Ekurhuleni",
            ["ekurhuleni"] = "Ekurhuleni"
        };


        // Main towns with their province and approximate centre. The GeoNames ZA.txt file has NO province or
        // municipality columns (only place name, postal code, latitude, longitude), so each place is attached to the
        // nearest town in this list. That gives a sensible Province -> Town -> Suburb picker; it can be a little off
        // right on a provincial border.
        private static readonly (string Town, string Province, double Lat, double Lon)[] Anchors =
        {
            ("Johannesburg", "Gauteng", -26.20, 28.05),
            ("Pretoria", "Gauteng", -25.75, 28.19),
            ("Ekurhuleni", "Gauteng", -26.18, 28.17),
            ("Vereeniging", "Gauteng", -26.67, 27.93),
            ("Krugersdorp", "Gauteng", -26.10, 27.77),
            ("Randfontein", "Gauteng", -26.18, 27.70),
            ("Heidelberg", "Gauteng", -26.50, 28.36),
            ("Springs", "Gauteng", -26.25, 28.44),
            ("Centurion", "Gauteng", -25.86, 28.19),
            ("Soweto", "Gauteng", -26.27, 27.86),
            ("Cape Town", "Western Cape", -33.92, 18.42),
            ("Stellenbosch", "Western Cape", -33.93, 18.86),
            ("Paarl", "Western Cape", -33.73, 18.97),
            ("Worcester", "Western Cape", -33.65, 19.45),
            ("George", "Western Cape", -33.96, 22.46),
            ("Mossel Bay", "Western Cape", -34.18, 22.14),
            ("Oudtshoorn", "Western Cape", -33.59, 22.20),
            ("Knysna", "Western Cape", -34.04, 23.05),
            ("Hermanus", "Western Cape", -34.42, 19.24),
            ("Saldanha", "Western Cape", -33.01, 17.94),
            ("Malmesbury", "Western Cape", -33.46, 18.73),
            ("Swellendam", "Western Cape", -34.02, 20.44),
            ("Beaufort West", "Western Cape", -32.36, 22.58),
            ("Caledon", "Western Cape", -34.23, 19.43),
            ("Plettenberg Bay", "Western Cape", -34.05, 23.37),
            ("Montagu", "Western Cape", -33.79, 20.12),
            ("Clanwilliam", "Western Cape", -32.18, 18.89),
            ("Laingsburg", "Western Cape", -33.20, 20.86),
            ("Gqeberha", "Eastern Cape", -33.96, 25.60),
            ("East London", "Eastern Cape", -33.02, 27.91),
            ("Mthatha", "Eastern Cape", -31.59, 28.78),
            ("Komani", "Eastern Cape", -31.90, 26.88),
            ("Makhanda", "Eastern Cape", -33.31, 26.52),
            ("Graaff-Reinet", "Eastern Cape", -32.25, 24.54),
            ("Port Alfred", "Eastern Cape", -33.59, 26.89),
            ("Jeffreys Bay", "Eastern Cape", -34.05, 24.91),
            ("Kariega", "Eastern Cape", -33.76, 25.40),
            ("Cradock", "Eastern Cape", -32.17, 25.62),
            ("Aliwal North", "Eastern Cape", -30.69, 26.71),
            ("Butterworth", "Eastern Cape", -32.33, 28.15),
            ("Humansdorp", "Eastern Cape", -34.03, 24.77),
            ("Port St Johns", "Eastern Cape", -31.62, 29.54),
            ("Bhisho", "Eastern Cape", -32.85, 27.44),
            ("Qonce", "Eastern Cape", -32.88, 27.39),
            ("Somerset East", "Eastern Cape", -32.72, 25.59),
            ("Maclear", "Eastern Cape", -31.07, 28.35),
            ("Lusikisiki", "Eastern Cape", -31.37, 29.58),
            ("Durban", "KwaZulu-Natal", -29.86, 31.02),
            ("Pietermaritzburg", "KwaZulu-Natal", -29.60, 30.38),
            ("Richards Bay", "KwaZulu-Natal", -28.78, 32.04),
            ("Newcastle", "KwaZulu-Natal", -27.76, 29.93),
            ("Ladysmith", "KwaZulu-Natal", -28.56, 29.78),
            ("Port Shepstone", "KwaZulu-Natal", -30.74, 30.45),
            ("Empangeni", "KwaZulu-Natal", -28.75, 31.90),
            ("Ulundi", "KwaZulu-Natal", -28.33, 31.42),
            ("Vryheid", "KwaZulu-Natal", -27.77, 30.79),
            ("KwaDukuza", "KwaZulu-Natal", -29.33, 31.29),
            ("Estcourt", "KwaZulu-Natal", -29.00, 29.88),
            ("Kokstad", "KwaZulu-Natal", -30.55, 29.42),
            ("Margate", "KwaZulu-Natal", -30.86, 30.37),
            ("Pongola", "KwaZulu-Natal", -27.38, 31.62),
            ("Dundee", "KwaZulu-Natal", -28.17, 30.23),
            ("Eshowe", "KwaZulu-Natal", -28.89, 31.47),
            ("Howick", "KwaZulu-Natal", -29.48, 30.23),
            ("Underberg", "KwaZulu-Natal", -29.79, 29.50),
            ("Bloemfontein", "Free State", -29.12, 26.21),
            ("Welkom", "Free State", -27.98, 26.73),
            ("Kroonstad", "Free State", -27.65, 27.23),
            ("Bethlehem", "Free State", -28.23, 28.31),
            ("Sasolburg", "Free State", -26.81, 27.82),
            ("Parys", "Free State", -26.90, 27.46),
            ("Harrismith", "Free State", -28.27, 29.13),
            ("Ficksburg", "Free State", -28.87, 27.89),
            ("Phuthaditjhaba", "Free State", -28.53, 28.82),
            ("Bothaville", "Free State", -27.38, 26.62),
            ("Zastron", "Free State", -30.30, 27.09),
            ("Heilbron", "Free State", -27.28, 27.97),
            ("Bethulie", "Free State", -30.50, 25.99),
            ("Trompsburg", "Free State", -30.03, 25.76),
            ("Kimberley", "Northern Cape", -28.74, 24.77),
            ("Upington", "Northern Cape", -28.45, 21.26),
            ("Springbok", "Northern Cape", -29.66, 17.89),
            ("De Aar", "Northern Cape", -30.65, 24.01),
            ("Kuruman", "Northern Cape", -27.46, 23.43),
            ("Calvinia", "Northern Cape", -31.47, 19.78),
            ("Postmasburg", "Northern Cape", -28.33, 23.06),
            ("Kathu", "Northern Cape", -27.70, 23.05),
            ("Colesberg", "Northern Cape", -30.72, 25.10),
            ("Alexander Bay", "Northern Cape", -28.60, 16.48),
            ("Carnarvon", "Northern Cape", -30.97, 22.13),
            ("Prieska", "Northern Cape", -29.66, 22.74),
            ("Hartswater", "Northern Cape", -27.75, 24.80),
            ("Victoria West", "Northern Cape", -31.40, 23.12),
            ("Pofadder", "Northern Cape", -29.13, 19.40),
            ("Sutherland", "Northern Cape", -32.40, 20.67),
            ("Mahikeng", "North West", -25.86, 25.64),
            ("Rustenburg", "North West", -25.67, 27.24),
            ("Potchefstroom", "North West", -26.72, 27.10),
            ("Klerksdorp", "North West", -26.85, 26.67),
            ("Brits", "North West", -25.63, 27.78),
            ("Lichtenburg", "North West", -26.15, 26.16),
            ("Vryburg", "North West", -26.96, 24.73),
            ("Zeerust", "North West", -25.54, 26.08),
            ("Taung", "North West", -27.53, 24.78),
            ("Ventersdorp", "North West", -26.31, 26.80),
            ("Wolmaransstad", "North West", -27.20, 25.98),
            ("Mogwase", "North West", -25.28, 27.21),
            ("Hartbeespoort", "North West", -25.74, 27.89),
            ("Polokwane", "Limpopo", -23.90, 29.45),
            ("Tzaneen", "Limpopo", -23.83, 30.16),
            ("Mokopane", "Limpopo", -24.19, 29.01),
            ("Thohoyandou", "Limpopo", -22.95, 30.48),
            ("Musina", "Limpopo", -22.34, 30.04),
            ("Lephalale", "Limpopo", -23.67, 27.74),
            ("Bela-Bela", "Limpopo", -24.88, 28.29),
            ("Phalaborwa", "Limpopo", -23.94, 31.14),
            ("Makhado", "Limpopo", -23.05, 29.91),
            ("Modimolle", "Limpopo", -24.70, 28.40),
            ("Giyani", "Limpopo", -23.30, 30.72),
            ("Burgersfort", "Limpopo", -24.67, 30.32),
            ("Groblersdal", "Limpopo", -25.17, 29.40),
            ("Hoedspruit", "Limpopo", -24.35, 30.95),
            ("Mookgophong", "Limpopo", -24.52, 28.72),
            ("Jane Furse", "Limpopo", -24.75, 29.82),
            ("Marble Hall", "Limpopo", -24.97, 29.30),
            ("Mbombela", "Mpumalanga", -25.47, 30.97),
            ("Emalahleni", "Mpumalanga", -25.87, 29.23),
            ("Middelburg", "Mpumalanga", -25.77, 29.46),
            ("Secunda", "Mpumalanga", -26.55, 29.17),
            ("Standerton", "Mpumalanga", -26.95, 29.25),
            ("Ermelo", "Mpumalanga", -26.53, 29.98),
            ("Barberton", "Mpumalanga", -25.79, 31.05),
            ("Piet Retief", "Mpumalanga", -27.00, 30.81),
            ("Mashishing", "Mpumalanga", -25.10, 30.46),
            ("Bethal", "Mpumalanga", -26.46, 29.46),
            ("Hazyview", "Mpumalanga", -25.05, 31.13),
            ("Komatipoort", "Mpumalanga", -25.43, 31.95),
            ("Sabie", "Mpumalanga", -25.10, 30.78),
            ("Volksrust", "Mpumalanga", -27.37, 29.88),
            ("Malelane", "Mpumalanga", -25.49, 31.51),
            ("Graskop", "Mpumalanga", -24.94, 30.85),
            ("Delmas", "Mpumalanga", -26.15, 28.68),
            ("KwaMhlanga", "Mpumalanga", -25.43, 28.95),
            ("Carolina", "Mpumalanga", -26.07, 30.12),
        };

        // Only the big cities swallow the places around them (places within 30 km are listed as that
        // city's suburbs, e.g. Walmer -> Gqeberha, Hatfield -> Pretoria). Every other place is its own town.
        // The nearest main town is always used to work out the PROVINCE, because the file has none.
        private static readonly HashSet<string> Metros = new(StringComparer.OrdinalIgnoreCase)
        {
            "Johannesburg", "Pretoria", "Ekurhuleni", "Soweto", "Cape Town", "Durban", "Gqeberha", "East London",
            "Bloemfontein", "Pietermaritzburg", "Centurion"
        };

        private static readonly HashSet<string> NotPlaces = new()
        {
            "easterncape", "freestate", "gauteng", "kwazulunatal", "limpopo", "mpumalanga", "northwest", "northernwest",
            "northerncape", "westerncape", "southafrica", "ec", "fs", "gt", "kzn", "lp", "mp", "nw", "nc", "wc"
        };

        private static (string Province, string? MetroTown)? Nearest(string latText, string lonText)
        {
            if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return null;
            if (!double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return null;
            if (lat == 0 && lon == 0) return null;
            double best = double.MaxValue; var pick = Anchors[0];
            foreach (var a in Anchors)
            {
                var dLat = a.Lat - lat;
                var dLon = (a.Lon - lon) * Math.Cos(lat * Math.PI / 180.0);
                var d = dLat * dLat + dLon * dLon;
                if (d < best) { best = d; pick = a; }
            }
            var km = Math.Sqrt(best) * 111.0;
            return (pick.Province, Metros.Contains(pick.Town) && km <= 30.0 ? pick.Town : null);
        }

        // Suburbs seeded by script 012 (kept when the list is re-imported).
        public static List<LocationRow> BuiltInSuburbs()
        {
            var list = new List<LocationRow>();
            void Add(string province, string town, params (string Name, string Postal)[] items)
            { foreach (var i in items) list.Add(new LocationRow { Province = province, Town = town, Suburb = i.Name, PostalCode = i.Postal }); }
            Add("Eastern Cape", "Gqeberha", ("Summerstrand", "6001"), ("Central", "6001"), ("Mill Park", "6001"), ("Humewood", "6001"), ("Walmer", "6070"),
                ("Lorraine", "6070"), ("Charlo", "6070"), ("Newton Park", "6045"), ("Korsten", "6020"), ("North End", "6056"), ("Westering", "6025"),
                ("Fairview", "6011"), ("Sydenham", "6001"), ("Linton Grange", "6059"), ("Kabega Park", "6025"));
            Add("Eastern Cape", "East London", ("Vincent", "5247"), ("Beacon Bay", "5241"));
            Add("Free State", "Bloemfontein", ("Westdene", "9301"), ("Universitas", "9321"));
            Add("Gauteng", "Johannesburg", ("Sandton", "2196"), ("Rosebank", "2196"), ("Soweto", "1818"));
            Add("Gauteng", "Pretoria", ("Hatfield", "0028"), ("Centurion", "0157"));
            Add("KwaZulu-Natal", "Durban", ("Umhlanga", "4319"), ("Berea", "4001"));
            Add("Limpopo", "Polokwane", ("Bendor", "0699"));
            Add("Mpumalanga", "Mbombela", ("Nelspruit CBD", "1200"));
            Add("North West", "Mahikeng", ("Mahikeng CBD", "2745"));
            Add("Northern Cape", "Kimberley", ("Kimberley CBD", "8301"));
            Add("Western Cape", "Cape Town", ("Sea Point", "8005"), ("Claremont", "7708"));
            return list;
        }

        public static string CleanPostal(string? code)
        {
            var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0 || digits.Length > 4) return "";
            return digits.PadLeft(4, '0');
        }

        private static string Cut(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

        public static List<LocationRow> Parse(TextReader reader)
        {
            var rows = new List<LocationRow>();
            string? line;
            string[]? csvHeader = null;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var tab = line.Split('\t');

                // GeoNames: country, postal, place, admin1(province), code, admin2(municipality), code, admin3, code, lat, lon, accuracy
                if (tab.Length >= 6 && tab[0].Trim().Length == 2 && tab[0].Trim().All(char.IsLetter))
                {
                    var postal = CleanPostal(tab[1]);
                    var place = tab[2].Trim();
                    var province = tab[3].Trim();
                    var town = CleanTownName(tab[5]);
                    if (town.Length == 0 && tab.Length > 7) town = CleanTownName(tab[7]);
                    if (NotPlaces.Contains(Norm(place)) || Norm(place).Length < 2) continue;   // junk rows such as "Eastern Cape"
                    var friendly = CleanTownName(place);                                        // Port Elizabeth -> Gqeberha
                    if (province.Length == 0 && tab.Length > 10)
                    {
                        // Real ZA.txt: admin columns are empty -> work the province out from the coordinates.
                        var near = Nearest(tab[9], tab[10]);
                        if (near != null) { province = near.Value.Province; town = near.Value.MetroTown ?? friendly; }
                    }
                    if (town.Length == 0) town = friendly;
                    place = friendly;
                    if (postal.Length == 0 || place.Length == 0 || province.Length == 0) continue;
                    rows.Add(new LocationRow { Province = province, Town = Cut(town, 100), Suburb = Cut(place, 100), PostalCode = postal });
                    continue;
                }

                // CSV with header
                var cells = SplitCsv(line);
                if (csvHeader == null)
                {
                    var h = cells.Select(c => Norm(c)).ToArray();
                    if (h.Contains("province") && h.Contains("suburb")) { csvHeader = h; }
                    continue;
                }
                string Get(params string[] names)
                {
                    foreach (var n in names)
                    {
                        var i = Array.IndexOf(csvHeader, n);
                        if (i >= 0 && i < cells.Count) return cells[i].Trim();
                    }
                    return "";
                }
                var p2 = CleanPostal(Get("postalcode", "postcode", "postal", "code"));
                var sub = Get("suburb");
                var prov = Get("province");
                var tw = CleanTownName(Get("town", "city", "townorcity"));
                if (tw.Length == 0) tw = sub;
                if (p2.Length == 0 || sub.Length == 0 || prov.Length == 0) continue;
                rows.Add(new LocationRow { Province = prov, Town = Cut(tw, 100), Suburb = Cut(sub, 100), PostalCode = p2 });
            }
            // The same name with several postal codes (e.g. one postal town with 50 codes): label each one
            // "Name (postal)" so the person can pick the right code.
            var multi = rows.GroupBy(r => (r.Province, r.Town, Norm(r.Suburb)))
                            .Where(g => g.Select(x => x.PostalCode).Distinct().Count() > 1)
                            .SelectMany(g => g).ToHashSet();
            foreach (var r in multi) r.Suburb = Cut(r.Suburb, 90) + " (" + r.PostalCode + ")";
            return rows;
        }

        private static List<string> SplitCsv(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else sb.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}
