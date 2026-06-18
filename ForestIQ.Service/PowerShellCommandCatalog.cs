using ForestIQ.Domain.DTO;

namespace ForestIQ.Service
{
    public static class PowerShellCommandCatalog
    {
        public static readonly Dictionary<string, PowerShellCommandDefinition> Commands = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GetADDomain"] = new()
            {
                Key = "GetADDomain",
                Name = "Get AD Domain",
                Description = "Returns Active Directory domain information.",
                Category = "Active Directory",
                Command = "Import-Module ActiveDirectory; Get-ADDomain"
            },

            ["GetADForest"] = new()
            {
                Key = "GetADForest",
                Name = "Get AD Forest",
                Description = "Returns Active Directory forest information.",
                Category = "Active Directory",
                Command = "Import-Module ActiveDirectory; Get-ADForest | Select-Object *"
            },

            ["GetReplicationFailures"] = new()
            {
                Key = "GetReplicationFailures",
                Name = "Get Replication Failures",
                Description = "Returns AD replication failures.",
                Category = "Active Directory",
                Command = """
                          Import-Module ActiveDirectory

                          $forest = Get-ADForest

                          Get-ADReplicationFailure `
                              -Target $forest.RootDomain `
                              -Scope Forest |
                          Select-Object `
                              FailureCount,
                              FirstFailureTime,
                              LastError,
                              Partner,
                              Server
                          """
            },

            ["GetServices"] = new()
            {
                Key = "GetServices",
                Name = "Get Services",
                Description = "Returns Windows services.",
                Category = "System",
                Command = "Get-Service | Select Name, Status"
            },

            ["GetProcesses"] = new()
            {
                Key = "GetProcesses",
                Name = "Get Processes",
                Description = "Returns running processes.",
                Category = "System",
                Command = "Get-Process | Select ProcessName, Id"
            },

            ["GetComputerInfo"] = new()
            {
                Key = "GetComputerInfo",
                Name = "Get Computer Info",
                Description = "Returns machine information.",
                Category = "System",
                Command = "Get-ComputerInfo"
            },

            ["GetADUsers"] = new()
            {
                Key = "GetADUsers",
                Name = "Get AD Users",
                Description = "Returns Active Directory users.",
                Category = "Active Directory",
                Command = """
                    Import-Module ActiveDirectory;
                    Get-ADUser -Filter * -Properties DisplayName, EmailAddress |
                    Select-Object SamAccountName, DisplayName, EmailAddress
              """
            },

            ["GetDomainControllers"] = new()
            {
                Key = "GetDomainControllers",
                Name = "Get Domain Controllers",
                Description = "Returns all domain controllers in the domain.",
                Category = "Domain Controller",
                Command = "Import-Module ActiveDirectory; Get-ADDomainController -Filter * | Select HostName,Site,IPv4Address,OperatingSystem"
            },
            ["GetFSMORoles"] = new()
            {
                Key = "GetFSMORoles",
                Name = "Get FSMO Roles",
                Description = "Returns FSMO role owners.",
                Category = "Domain Controller",
                Command = """
                            Import-Module ActiveDirectory;

                            [pscustomobject]@{
                                SchemaMaster         = (Get-ADForest).SchemaMaster
                                DomainNamingMaster   = (Get-ADForest).DomainNamingMaster
                                PDCEmulator          = (Get-ADDomain).PDCEmulator
                                RIDMaster            = (Get-ADDomain).RIDMaster
                                InfrastructureMaster = (Get-ADDomain).InfrastructureMaster
                            }
                            """
            },

            ["GetADSite"] = new()
            {
                Key = "GetADSite",
                Name = "Get AD Site",
                Description = "Returns the Active Directory site of the server.",
                Category = "Domain Controller",
                Command = @"$site = (nltest /dsgetsite)[0]
                    [pscustomobject]@{
                        SiteName = $site
                    }"
            },

            ["DcDiagSummary"] = new()
            {
                Key = "DcDiagSummary",
                Name = "DC Diagnostics Summary",
                Description = "Returns DC health check results in structured format.",
                Category = "Domain Controller",
                Command = """
                            $report = dcdiag

                            $tests = @()

                            foreach ($line in $report)
                            {
                                if ($line -match "passed test (.+)")
                                {
                                    $tests += [pscustomobject]@{
                                        Name   = $matches[1].Trim()
                                        Status = "Passed"
                                    }
                                }
                                elseif ($line -match "failed test (.+)")
                                {
                                    $tests += [pscustomobject]@{
                                        Name   = $matches[1].Trim()
                                        Status = "Failed"
                                    }
                                }
                            }

                            [pscustomobject]@{
                                TotalTests  = $tests.Count
                                PassedTests = ($tests | Where-Object Status -eq 'Passed').Count
                                FailedTests = ($tests | Where-Object Status -eq 'Failed').Count
                                Tests       = $tests
                            }
                          """
            },

            ["GetFSMORolesNetdom"] = new()
            {
                Key = "GetFSMORolesNetdom",
                Name = "Get FSMO Roles (Netdom)",
                Description = "Returns FSMO role owners.",
                Category = "Domain Controller",
                Command = @"$output = netdom query fsmo
                    [pscustomobject]@{
                        SchemaMaster = ($output | Select-String ""Schema master"").ToString().Split(':')[-1].Trim()
                    }"
            },

            ["GetActiveADUsers"] = new()
            {
                Key = "GetActiveADUsers",
                Name = "Get Active AD Users",
                Description = "Returns enabled Active Directory users.",
                Category = "Active Directory",
                Command = """
                            Import-Module ActiveDirectory;
                            Get-ADUser -Filter 'Enabled -eq $true' `
                                -Properties DisplayName, EmailAddress |
                            Select-Object SamAccountName, DisplayName, EmailAddress
                            """
            },

            ["GetADUserDetails"] = new()
            {
                Key = "GetADUserDetails",
                Name = "Get AD User Details",
                Description = "Returns detailed AD user information.",
                Category = "Active Directory",
                Command = """
                        Import-Module ActiveDirectory;

                        Get-ADUser -Filter * -Properties * |
                        Select-Object `
                            SamAccountName,
                            UserPrincipalName,
                            DisplayName,
                            GivenName,
                            Surname,
                            EmailAddress,
                            Department,
                            Title,
                            Company,
                            Enabled,
                            LockedOut,
                            LastLogonDate,
                            PasswordLastSet,
                            Created
                        """
            },
            ["GetADSiteDetails"] = new()
            {
                Key = "GetADSiteDetails",
                Name = "Get AD Site",
                Description = "Returns Active Directory site information.",
                Category = "Domain Controller",
                Command = """
                     Import-Module ActiveDirectory

                     $dc = Get-ADDomainController -Discover

                     [pscustomobject]@{
                         SiteName       = $dc.Site
                         HostName       = $dc.HostName
                         IPv4Address    = $dc.IPv4Address
                         Forest         = $dc.Forest
                         Domain         = $dc.Domain
                         OperatingSystem= $dc.OperatingSystem
                     }
                """
            },
            ["GetADHealthReport"] = new()
            {
                Key = "GetADHealthReport",
                Name = "AD Health Report",
                Description = "Returns complete AD discovery and health report.",
                Category = "Health",
                Command = PowerShellService.GetAdHealthReportScript()
            }
        };

    }
}