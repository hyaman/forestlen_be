using ForestIQ.Domain;
using ForestIQ.Domain.Interface;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace ForestIQ.Service
{
    public class KerberosService : IKerberosService
    {
        private readonly ILogger<KerberosService> _logger;

        public KerberosService(ILogger<KerberosService> logger)
        {
            _logger = logger;
        }

        public string BuildPrincipal(string domainName, string userName)
        {
            var domain = NormalizeDomain(domainName);
            var user = userName.Contains('@')
                ? userName.Split('@')[0]
                : userName.Contains('\\')
                    ? userName.Split('\\')[^1]
                    : userName;

            return $"{user}@{domain}";
        }

        public string BuildCachePath(string sessionKey)
        {
            return $"/tmp/krb5cc-{sessionKey}";
        }

        public bool IsIpAddress(string host)
        {
            return IPAddress.TryParse(host, out _);
        }

        public async Task<(bool Success, string? Error)> AcquireTicketAsync(string domainName,string userName,string password,string cachePath)
        {
            var principal = BuildPrincipal(domainName, userName);
            var lowerDomain = NormalizeDomain(domainName);
            var realm = lowerDomain.ToUpperInvariant();

            var krb5ConfigPath = Path.Combine(Path.GetTempPath(), $"krb5-{Guid.NewGuid():N}.conf");

            try
            {
                var krb5ConfigContent = BuildKrb5Config(realm, lowerDomain);
                _logger.LogInformation("Kerberos Step 1: Writing krb5.conf to {Path} with KDCs: {KDCs}", krb5ConfigPath, string.Join(", ", Runtime.ActiveDirectory.DnsServers));
                await File.WriteAllTextAsync(krb5ConfigPath, krb5ConfigContent);

                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/kinit",
                    Arguments = $"-c FILE:{cachePath} {principal}",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Point kinit at the per-request config; no global /etc/krb5.conf needed
                startInfo.Environment["KRB5_CONFIG"] = krb5ConfigPath;

                _logger.LogInformation("Kerberos Step 2: Starting kinit process for {Principal}...", principal);

                using var process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Failed to start kinit.");

                // Drain stdout/stderr BEFORE writing stdin to avoid deadlocks and to
                // ensure kinit's real error message is captured even if it exits early.
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // If kinit exits before reading (e.g. bad realm/DNS) the pipe breaks —
                // swallow the IOException so stderr is surfaced instead.
                try
                {
                    await process.StandardInput.WriteLineAsync(password);
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(
                        ex,
                        "kinit closed stdin early for {Principal} — process likely exited before reading password.",
                        principal);
                }
                finally
                {
                    try { process.StandardInput.Close(); } catch { /* already closed */ }
                }

                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                _logger.LogInformation("Kerberos Step 3: kinit process exited with code {ExitCode}. Output: {Output} | Error: {Error}", process.ExitCode, output.Trim(), error.Trim());

                if (process.ExitCode != 0)
                {
                    var message = string.IsNullOrWhiteSpace(error) ? output : error;
                    _logger.LogWarning(
                        "kinit failed for {Principal}: {Message}",
                        principal,
                        message.Trim());

                    return (false, message.Trim());
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kerberos ticket acquisition failed for {Principal}.", principal);
                return (false, ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(krb5ConfigPath))
                    {
                        File.Delete(krb5ConfigPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary krb5.conf at {Path}.", krb5ConfigPath);
                }
            }
        }

        public void DestroyTicket(string? cachePath)
        {
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                return;
            }

            try
            {
                if (File.Exists(cachePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/kdestroy",
                        Arguments = $"-c FILE:{cachePath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    process?.WaitForExit();

                    File.Delete(cachePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to destroy Kerberos ticket cache at {CachePath}.", cachePath);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Builds a minimal krb5.conf for the given realm.
        /// KDC IPs come from Runtime.ActiveDirectory.DnsServers (appsettings).
        /// In an AD environment the DNS servers are also the domain controllers / KDCs.
        /// Falls back to the domain name itself if no IPs are configured.
        /// </summary>
        private string BuildKrb5Config(string realm, string lowerDomain)
        {
            var dnsServers = Runtime.ActiveDirectory.DnsServers;

            var sb = new StringBuilder();
            sb.AppendLine("[libdefaults]");
            sb.AppendLine($"    default_realm = {realm}");
            sb.AppendLine("    dns_lookup_realm = false");
            // Use explicit KDC IPs when available; fall back to DNS SRV discovery.
            sb.AppendLine($"    dns_lookup_kdc = {(dnsServers.Count == 0 ? "true" : "false")}");
            sb.AppendLine("    rdns = false");
            sb.AppendLine();
            sb.AppendLine("[realms]");
            sb.AppendLine($"    {realm} = {{");

            if (dnsServers.Count > 0)
            {
                foreach (var ip in dnsServers)
                {
                    sb.AppendLine($"        kdc = {ip}");
                }

                sb.AppendLine($"        admin_server = {dnsServers[0]}");
            }
            else
            {
                // No IPs configured — use domain name and rely on DNS SRV records
                sb.AppendLine($"        kdc = {lowerDomain}");
                sb.AppendLine($"        admin_server = {lowerDomain}");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("[domain_realm]");
            sb.AppendLine($"    .{lowerDomain} = {realm}");
            sb.AppendLine($"    {lowerDomain} = {realm}");

            return sb.ToString();
        }

        private static string NormalizeDomain(string domainName)
        {
            var domain = domainName.Trim();

            if (domain.Contains('\\'))
            {
                domain = domain.Split('\\')[0];
            }

            return domain.ToUpperInvariant();
        }

        #endregion
    }
}
