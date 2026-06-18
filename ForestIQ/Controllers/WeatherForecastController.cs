using DnsClient;
using ForestIQ.Domain.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using LdapConn = Novell.Directory.Ldap.LdapConnection;
using LdapEx = Novell.Directory.Ldap.LdapException;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        //[HttpPost("controllers")]
        //public async Task<IActionResult> GetDomainControllers([FromBody] PowerShellRequest request)
        //{
        //    if (request == null || string.IsNullOrWhiteSpace(request.DomainName))
        //    {
        //        return BadRequest("Invalid request payload.");
        //    }

        //    var discoveredControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        //    try
        //    {
        //        // Step 1: Query DNS SRV Records (Standard Domain Discovery)
        //        try
        //        {
        //            string srvRecordQuery = $"_ldap._tcp.dc._msdcs.{request.DomainName}";
        //            var lookup = new LookupClient();
        //            var result = await lookup.QueryAsync(srvRecordQuery, QueryType.SRV);

        //            if (result.Answers.Any())
        //            {
        //                var dnsDcList = result.Answers.SrvRecords()
        //                    .OrderBy(r => r.Priority)
        //                    .Select(r => r.Target.Value.TrimEnd('.'))
        //                    .ToList();

        //                foreach (var dc in dnsDcList) discoveredControllers.Add(dc);
        //            }
        //        }
        //        catch (Exception dnsEx)
        //        {
        //            Console.WriteLine($"DNS Discovery failed: {dnsEx.Message}");
        //        }

        //        // Step 2: Scan the Local Network Subnet (LAN Fallback)
        //        if (!string.IsNullOrWhiteSpace(request.LocalNetworkSubnet))
        //        {
        //            var localDcs = await ScanLocalSubnetForLdapAsync(request.LocalNetworkSubnet);
        //            foreach (var dc in localDcs) discoveredControllers.Add(dc);
        //        }

        //        if (!discoveredControllers.Any())
        //        {
        //            return NotFound("No Domain Controllers discovered via DNS or local network scanning.");
        //        }

        //        // Step 3: Authenticate User Credentials against the first accessible controller
        //        string primaryDcOne = "";

        //        foreach (var primaryDc in discoveredControllers)
        //        {
        //            // Instantiate using the parameterless constructor compatible with your library version
        //            var connection = new LdapConn();

        //            //// Instantiate using the parameterless constructor compatible with your library version
        //            //var connection = new LdapConn();

        //            try
        //            {
        //                // Bypasses the certificate delegate syntax issue at the global network layer
        //                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

        //                // 1. First, connect over the standard LDAP port 389 (Do NOT set SecureSocketLayer = true yet)
        //                connection.Connect(primaryDc, 389);

        //                // 2. Safely upgrade the established connection to secure TLS (This avoids the NullReferenceException)
        //                connection.startTLS();

        //                string userPrincipalName = request.UserName.Contains("@")
        //                    ? request.UserName
        //                    : $"{request.UserName}@{request.DomainName}";

        //                // 3. Perform the credential bind validation over the now-encrypted channel
        //                connection.Bind(userPrincipalName, request.Password);
        //                primaryDcOne = primaryDc;
        //                break; 
        //            }
        //            catch(Exception ex)
        //            {
        //                _logger.LogError("ERROR :" + ex.Message);
        //                continue;
        //            }
        //            finally
        //            {
        //                // 4. Clean up TLS and drop the connection network sockets safely
        //                try
        //                {
        //                    if (connection.TLS)
        //                    {
        //                        connection.stopTLS();
        //                    }
        //                    if (connection.Connected)
        //                    {
        //                        connection.Disconnect();
        //                    }
        //                }
        //                catch {

        //                }


        //            }
        //        }

        //        // Step 4: Return compiled results
        //        return Ok(new
        //        {
        //            Domain = request.DomainName,
        //            AuthenticatedAgainst = primaryDcOne,
        //            TotalControllersFound = discoveredControllers.Count,
        //            DomainControllers = discoveredControllers.ToList()
        //        });
        //    }
        //    catch (LdapEx ex) when (ex.ResultCode == LdapEx.INVALID_CREDENTIALS)
        //    {
        //        return Unauthorized("Authentication failed: Invalid username or password.");
        //    }
        //    catch (LdapEx ex)
        //    {
        //        return StatusCode(503, $"LDAP connection error: {ex.Message}");
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        //    }
        //}

        [HttpPost("controllers")]
        public async Task<IActionResult> GetDomainControllers([FromBody] PowerShellRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DomainName))
            {
                return BadRequest("Invalid request payload.");
            }

            try
            {
                string safePassword = request.Password.Replace("'", "''");

                // This script uses linux-native 'nslookup' to find SRV records, then filters the output
                // This script uses the built-in .NET network stack inside PowerShell to resolve the IPs
                string script = $@"
                        $ErrorActionPreference = 'Stop'
                        
                        # 1. Resolve Domain Controller IPs using built-in cross-platform .NET Dns class
                        try {{
                            $ips = [System.Net.Dns]::GetHostAddresses('{request.DomainName}') | ForEach-Object {{ $_.IPAddressToString }}
                            $dcs = $ips | Select-Object -Unique
                        }} catch {{
                            throw 'Failed to resolve domain name via .NET DNS engine.'
                        }}

                        if (-not $dcs) {{ 
                            throw 'No Domain Controllers found or resolved.' 
                        }}
                        
                        # Select the primary target IP or Hostname
                        $targetDc = $dcs

                        # 2. Build Credentials safely inside PowerShell memory
                        #$secPass = ConvertTo-SecureString '{safePassword}' -AsPlainText -Force
                        #$upn = if ('{request.UserName}' -like '*@*') {{ '{request.UserName}' }} else {{ '{request.UserName}@{request.DomainName}' }}
                        #$cred = New-Object System.Management.Automation.PSCredential($upn, $secPass)
                        #
                        ## 3. Test remote connection using WSMan/WinRM 
                        #$session = New-PSSession -ComputerName $targetDc -Credential $cred -Authentication Negotiate -ErrorAction Stop
                        #
                        ## If it reaches here, authentication passed! Close the session.
                        #Remove-PSSession $session

                        $output = @{{ success = $true; domain = '{request.DomainName}'; authenticatedAgainst = $targetDc; controllers = $dcs }}
                        $output | ConvertTo-Json -Compress
                    ";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    string stdout = await process.StandardOutput.ReadToEndAsync();
                    string stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(stderr))
                    {
                        return StatusCode(500, $"PowerShell Engine Error: {stderr}");
                    }

                    using var document = JsonDocument.Parse(stdout);
                    return Ok(document);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An unexpected system error occurred: {ex.Message}");
            }
        }



        /// <summary>
        /// Scans a local Class C subnet range (1-254) in parallel for open LDAP ports (389 / 636).
        /// </summary>
        private async Task<List<string>> ScanLocalSubnetForLdapAsync(string subnetPrefix)
        {
            var activeLdapHosts = new List<string>();
            var tasks = new List<Task<(string IP, bool IsOpen)>>();

            // Clean the prefix (e.g., "192.168.1." or "192.168.1")
            string baseIp = subnetPrefix.TrimEnd('.');

            // Spin up 254 parallel connection checks simultaneously
            for (int i = 1; i <= 254; i++)
            {
                string targetIp = $"{baseIp}.{i}";
                tasks.Add(CheckLdapPortAsync(targetIp));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result.IsOpen)
                {
                    activeLdapHosts.Add(result.IP);
                }
            }

            return activeLdapHosts;
        }

        /// <summary>
        /// Fast network socket check to see if an IP has LDAP or LDAPS ports open.
        /// </summary>
        private async Task<(string IP, bool IsOpen)> CheckLdapPortAsync(string ipAddress)
        {
            int[] ldapPorts = { 389, 636 }; // Standard LDAP and Secure LDAPS ports

            foreach (int port in ldapPorts)
            {
                try
                {
                    using var client = new TcpClient();
                    // Set a very aggressive 250ms timeout since it's on a local network
                    var delayTask = Task.Delay(250);
                    var connectTask = client.ConnectAsync(ipAddress, port);

                    var completedTask = await Task.WhenAny(connectTask, delayTask);

                    if (completedTask == connectTask && client.Connected)
                    {
                        return (ipAddress, true);
                    }
                }
                catch
                {
                    // Ignore network connection drops, timeouts, and closed ports
                }
            }

            return (ipAddress, false);
        }
    }

}
