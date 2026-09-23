using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// The countries a player can pick for their profile, by ISO 3166-1 alpha-2 code ("PK", "US", "GB"...). The code is what is
    /// saved and sent to other players; the name is only for the picker. A country is never guessed (not from the name, the
    /// language or the IP address): the player chooses it, and "" means "not chosen" (no flag is shown).
    /// </summary>
    public static class Countries
    {
        public readonly struct Country
        {
            public readonly string Code;
            public readonly string Name;
            public Country(string code, string name) { Code = code; Name = name; }
        }

        // ISO 3166-1 alpha-2, English short names (plus XK Kosovo, which is widely used though not yet in the standard)
        static readonly string[] Data =
        {
            "AD", "Andorra", "AE", "United Arab Emirates", "AF", "Afghanistan", "AG", "Antigua and Barbuda", "AI", "Anguilla",
            "AL", "Albania", "AM", "Armenia", "AO", "Angola", "AQ", "Antarctica", "AR", "Argentina", "AS", "American Samoa",
            "AT", "Austria", "AU", "Australia", "AW", "Aruba", "AX", "Åland Islands", "AZ", "Azerbaijan",
            "BA", "Bosnia and Herzegovina", "BB", "Barbados", "BD", "Bangladesh", "BE", "Belgium", "BF", "Burkina Faso",
            "BG", "Bulgaria", "BH", "Bahrain", "BI", "Burundi", "BJ", "Benin", "BL", "Saint Barthélemy", "BM", "Bermuda",
            "BN", "Brunei", "BO", "Bolivia", "BQ", "Caribbean Netherlands", "BR", "Brazil", "BS", "Bahamas", "BT", "Bhutan",
            "BV", "Bouvet Island", "BW", "Botswana", "BY", "Belarus", "BZ", "Belize", "CA", "Canada", "CC", "Cocos (Keeling) Islands",
            "CD", "DR Congo", "CF", "Central African Republic", "CG", "Congo", "CH", "Switzerland", "CI", "Côte d'Ivoire",
            "CK", "Cook Islands", "CL", "Chile", "CM", "Cameroon", "CN", "China", "CO", "Colombia", "CR", "Costa Rica", "CU", "Cuba",
            "CV", "Cape Verde", "CW", "Curaçao", "CX", "Christmas Island", "CY", "Cyprus", "CZ", "Czechia", "DE", "Germany",
            "DJ", "Djibouti", "DK", "Denmark", "DM", "Dominica", "DO", "Dominican Republic", "DZ", "Algeria", "EC", "Ecuador",
            "EE", "Estonia", "EG", "Egypt", "EH", "Western Sahara", "ER", "Eritrea", "ES", "Spain", "ET", "Ethiopia", "FI", "Finland",
            "FJ", "Fiji", "FK", "Falkland Islands", "FM", "Micronesia", "FO", "Faroe Islands", "FR", "France", "GA", "Gabon",
            "GB", "United Kingdom", "GD", "Grenada", "GE", "Georgia", "GF", "French Guiana", "GG", "Guernsey", "GH", "Ghana",
            "GI", "Gibraltar", "GL", "Greenland", "GM", "Gambia", "GN", "Guinea", "GP", "Guadeloupe", "GQ", "Equatorial Guinea",
            "GR", "Greece", "GS", "South Georgia", "GT", "Guatemala", "GU", "Guam", "GW", "Guinea-Bissau", "GY", "Guyana",
            "HK", "Hong Kong", "HM", "Heard and McDonald Islands", "HN", "Honduras", "HR", "Croatia", "HT", "Haiti", "HU", "Hungary",
            "ID", "Indonesia", "IE", "Ireland", "IL", "Israel", "IM", "Isle of Man", "IN", "India", "IO", "British Indian Ocean Territory",
            "IQ", "Iraq", "IR", "Iran", "IS", "Iceland", "IT", "Italy", "JE", "Jersey", "JM", "Jamaica", "JO", "Jordan", "JP", "Japan",
            "KE", "Kenya", "KG", "Kyrgyzstan", "KH", "Cambodia", "KI", "Kiribati", "KM", "Comoros", "KN", "Saint Kitts and Nevis",
            "KP", "North Korea", "KR", "South Korea", "KW", "Kuwait", "KY", "Cayman Islands", "KZ", "Kazakhstan", "LA", "Laos",
            "LB", "Lebanon", "LC", "Saint Lucia", "LI", "Liechtenstein", "LK", "Sri Lanka", "LR", "Liberia", "LS", "Lesotho",
            "LT", "Lithuania", "LU", "Luxembourg", "LV", "Latvia", "LY", "Libya", "MA", "Morocco", "MC", "Monaco", "MD", "Moldova",
            "ME", "Montenegro", "MF", "Saint Martin", "MG", "Madagascar", "MH", "Marshall Islands", "MK", "North Macedonia",
            "ML", "Mali", "MM", "Myanmar", "MN", "Mongolia", "MO", "Macao", "MP", "Northern Mariana Islands", "MQ", "Martinique",
            "MR", "Mauritania", "MS", "Montserrat", "MT", "Malta", "MU", "Mauritius", "MV", "Maldives", "MW", "Malawi", "MX", "Mexico",
            "MY", "Malaysia", "MZ", "Mozambique", "NA", "Namibia", "NC", "New Caledonia", "NE", "Niger", "NF", "Norfolk Island",
            "NG", "Nigeria", "NI", "Nicaragua", "NL", "Netherlands", "NO", "Norway", "NP", "Nepal", "NR", "Nauru", "NU", "Niue",
            "NZ", "New Zealand", "OM", "Oman", "PA", "Panama", "PE", "Peru", "PF", "French Polynesia", "PG", "Papua New Guinea",
            "PH", "Philippines", "PK", "Pakistan", "PL", "Poland", "PM", "Saint Pierre and Miquelon", "PN", "Pitcairn Islands",
            "PR", "Puerto Rico", "PS", "Palestine", "PT", "Portugal", "PW", "Palau", "PY", "Paraguay", "QA", "Qatar", "RE", "Réunion",
            "RO", "Romania", "RS", "Serbia", "RU", "Russia", "RW", "Rwanda", "SA", "Saudi Arabia", "SB", "Solomon Islands",
            "SC", "Seychelles", "SD", "Sudan", "SE", "Sweden", "SG", "Singapore", "SH", "Saint Helena", "SI", "Slovenia",
            "SJ", "Svalbard and Jan Mayen", "SK", "Slovakia", "SL", "Sierra Leone", "SM", "San Marino", "SN", "Senegal", "SO", "Somalia",
            "SR", "Suriname", "SS", "South Sudan", "ST", "São Tomé and Príncipe", "SV", "El Salvador", "SX", "Sint Maarten",
            "SY", "Syria", "SZ", "Eswatini", "TC", "Turks and Caicos Islands", "TD", "Chad", "TF", "French Southern Territories",
            "TG", "Togo", "TH", "Thailand", "TJ", "Tajikistan", "TK", "Tokelau", "TL", "Timor-Leste", "TM", "Turkmenistan",
            "TN", "Tunisia", "TO", "Tonga", "TR", "Türkiye", "TT", "Trinidad and Tobago", "TV", "Tuvalu", "TW", "Taiwan",
            "TZ", "Tanzania", "UA", "Ukraine", "UG", "Uganda", "UM", "U.S. Outlying Islands", "US", "United States", "UY", "Uruguay",
            "UZ", "Uzbekistan", "VA", "Vatican City", "VC", "Saint Vincent and the Grenadines", "VE", "Venezuela",
            "VG", "British Virgin Islands", "VI", "U.S. Virgin Islands", "VN", "Vietnam", "VU", "Vanuatu", "WF", "Wallis and Futuna",
            "WS", "Samoa", "XK", "Kosovo", "YE", "Yemen", "YT", "Mayotte", "ZA", "South Africa", "ZM", "Zambia", "ZW", "Zimbabwe"
        };

        static Country[] all;
        static Dictionary<string, string> names;

        /// <summary>Every country, sorted by name.</summary>
        public static IReadOnlyList<Country> All
        {
            get
            {
                if (all == null)
                {
                    var list = new List<Country>(Data.Length / 2);
                    for (int i = 0; i + 1 < Data.Length; i += 2) list.Add(new Country(Data[i], Data[i + 1]));
                    list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    all = list.ToArray();
                }
                return all;
            }
        }

        static Dictionary<string, string> Names
        {
            get
            {
                if (names == null)
                {
                    names = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int i = 0; i + 1 < Data.Length; i += 2) names[Data[i]] = Data[i + 1];
                }
                return names;
            }
        }

        /// <summary>A known country code in the saved form (upper case) or "" for anything else (empty, unknown, garbage from the network).</summary>
        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            code = code.Trim().ToUpperInvariant();
            return code.Length == 2 && Names.ContainsKey(code) ? code : "";
        }

        public static bool IsKnown(string code) => Normalize(code).Length > 0;

        /// <summary>The country's name, or "" when the code is not a known country.</summary>
        public static string NameOf(string code) => Names.TryGetValue(Normalize(code), out var n) ? n : "";

        /// <summary>Does the country match what the player typed in the search box (name or code, any case)?</summary>
        public static bool Matches(Country c, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            search = search.Trim();
            return c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(c.Code, search, StringComparison.OrdinalIgnoreCase);
        }
    }
}
