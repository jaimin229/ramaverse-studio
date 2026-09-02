using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RamaverseStudio.Services.Licensing
{
    public enum LicenseTier
    {
        Free,
        Trial,
        Pro,
        Commercial
    }

    public class LicenseState
    {
        [JsonPropertyName("tier")]
        public LicenseTier Tier { get; set; } = LicenseTier.Free;

        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = "";

        [JsonPropertyName("registeredEmail")]
        public string RegisteredEmail { get; set; } = "";

        [JsonPropertyName("machineId")]
        public string MachineId { get; set; } = "";

        [JsonPropertyName("activatedDateUtc")]
        public DateTime ActivatedDateUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("expiresDateUtc")]
        public DateTime? ExpiresDateUtc { get; set; }
    }

    public class LicenseManager
    {
        private static readonly Lazy<LicenseManager> _instance = new(() => new LicenseManager());
        public static LicenseManager Instance => _instance.Value;

        private readonly string _licenseFilePath;
        private LicenseState _currentState = new();

        public LicenseTier Tier => _currentState.Tier;
        public bool IsPro => _currentState.Tier == LicenseTier.Pro || _currentState.Tier == LicenseTier.Commercial || IsActiveTrial;
        public string CurrentKey => _currentState.LicenseKey;
        public string RegisteredEmail => _currentState.RegisteredEmail;

        public bool IsActiveTrial
        {
            get
            {
                if (_currentState.Tier != LicenseTier.Trial) return false;
                if (!_currentState.ExpiresDateUtc.HasValue) return false;
                return DateTime.UtcNow <= _currentState.ExpiresDateUtc.Value;
            }
        }

        public int TrialDaysRemaining
        {
            get
            {
                if (!IsActiveTrial || !_currentState.ExpiresDateUtc.HasValue) return 0;
                var remaining = _currentState.ExpiresDateUtc.Value - DateTime.UtcNow;
                return Math.Max(0, (int)Math.Ceiling(remaining.TotalDays));
            }
        }

        public string MachineId { get; }

        public LicenseManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "RamaverseStudio");
            Directory.CreateDirectory(dir);
            _licenseFilePath = Path.Combine(dir, "license.dat");

            MachineId = ComputeMachineFingerprint();
            LoadLicense();
        }

        public bool ValidateAndActivate(string licenseKey, string email = "")
        {
            return ValidateAndActivateAsync(licenseKey, email).GetAwaiter().GetResult();
        }

        public async Task<bool> ValidateAndActivateAsync(string licenseKey, string email = "")
        {
            if (string.IsNullOrWhiteSpace(licenseKey)) return false;

            string trimmed = licenseKey.Trim();

            // 1. Check if it's an offline cryptographic key (RAMA-PRO-XXXX)
            if (trimmed.ToUpperInvariant().StartsWith("RAMA-") && VerifyKeyChecksum(trimmed.ToUpperInvariant()))
            {
                _currentState = new LicenseState
                {
                    Tier = trimmed.Contains("-COM-") ? LicenseTier.Commercial : LicenseTier.Pro,
                    LicenseKey = trimmed.ToUpperInvariant(),
                    RegisteredEmail = email,
                    MachineId = MachineId,
                    ActivatedDateUtc = DateTime.UtcNow,
                    ExpiresDateUtc = null
                };
                SaveLicense();
                return true;
            }

            // 2. Validate live against Gumroad API
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var content = new System.Net.Http.FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
                {
                    { "product_id", "H41zgxi2X9fPNTBh9x7yzA==" },
                    { "product_permalink", "ramaverse-studio-pro" },
                    { "license_key", trimmed },
                    { "increment_uses_count", "true" }
                });

                var response = await client.PostAsync("https://api.gumroad.com/v2/licenses/verify", content);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                    {
                        string buyerEmail = email;
                        if (doc.RootElement.TryGetProperty("purchase", out var purchase))
                        {
                            if (purchase.TryGetProperty("email", out var emailProp))
                            {
                                buyerEmail = emailProp.GetString() ?? email;
                            }
                        }

                        _currentState = new LicenseState
                        {
                            Tier = LicenseTier.Pro,
                            LicenseKey = trimmed,
                            RegisteredEmail = buyerEmail,
                            MachineId = MachineId,
                            ActivatedDateUtc = DateTime.UtcNow,
                            ExpiresDateUtc = null
                        };
                        SaveLicense();
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public void ActivateTrial(int durationDays = 7)
        {
            if (_currentState.Tier == LicenseTier.Pro || _currentState.Tier == LicenseTier.Commercial)
                return;

            _currentState = new LicenseState
            {
                Tier = LicenseTier.Trial,
                LicenseKey = "TRIAL-EVALUATION",
                RegisteredEmail = "trial@user",
                MachineId = MachineId,
                ActivatedDateUtc = DateTime.UtcNow,
                ExpiresDateUtc = DateTime.UtcNow.AddDays(durationDays)
            };

            SaveLicense();
        }

        public void Deactivate()
        {
            _currentState = new LicenseState
            {
                Tier = LicenseTier.Free,
                LicenseKey = "",
                RegisteredEmail = "",
                MachineId = MachineId
            };

            try
            {
                if (File.Exists(_licenseFilePath)) File.Delete(_licenseFilePath);
            }
            catch { }
        }

        public static bool VerifyKeyChecksum(string key)
        {
            try
            {
                // Structure: RAMA-[TIER]-[PAYLOAD]-[CHECKSUM]
                var parts = key.Split('-');
                if (parts.Length < 4) return false;

                string payload = string.Join("-", parts[0..^1]);
                string expectedChecksum = parts[^1];

                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload + "_RAMAVERSE_SECRET_SALT_2026"));
                string computedChecksum = Convert.ToHexString(hash)[..parts[^1].Length];

                return string.Equals(expectedChecksum, computedChecksum, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateKeyForTier(string tierPrefix, string randomPayload)
        {
            string baseKey = $"RAMA-{tierPrefix.ToUpperInvariant()}-{randomPayload.ToUpperInvariant()}";
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(baseKey + "_RAMAVERSE_SECRET_SALT_2026"));
            string checksum = Convert.ToHexString(hash)[..4];
            return $"{baseKey}-{checksum}";
        }

        private void SaveLicense()
        {
            try
            {
                string json = JsonSerializer.Serialize(_currentState);
                byte[] raw = Encoding.UTF8.GetBytes(json);
                // Windows DPAPI user encryption
                byte[] protectedBytes = ProtectedData.Protect(raw, Encoding.UTF8.GetBytes(MachineId), DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_licenseFilePath, protectedBytes);
            }
            catch { }
        }

        private void LoadLicense()
        {
            try
            {
                if (!File.Exists(_licenseFilePath)) return;

                byte[] protectedBytes = File.ReadAllBytes(_licenseFilePath);
                byte[] raw = ProtectedData.Unprotect(protectedBytes, Encoding.UTF8.GetBytes(MachineId), DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(raw);

                var loaded = JsonSerializer.Deserialize<LicenseState>(json);
                if (loaded != null && loaded.MachineId == MachineId)
                {
                    _currentState = loaded;
                }
            }
            catch
            {
                _currentState = new LicenseState();
            }
        }

        private static string ComputeMachineFingerprint()
        {
            try
            {
                string cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU_GENERIC";
                string machine = Environment.MachineName;
                string user = Environment.UserName;
                string raw = $"{cpu}_{machine}_{user}";

                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToHexString(hash)[..16];
            }
            catch
            {
                return "RAMA-HW-UNKNOWN";
            }
        }
    }
}
