                Import-Module ActiveDirectory -ErrorAction Stop

                $username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
                $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
                $cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

                $forest = Get-ADForest -Credential $cred

                foreach ($domain in $forest.Domains)
                {
                    try {
                        Get-ADDomainController -Filter * -Server $domain -Credential $cred -ErrorAction Stop |
                            Select-Object `
                                @{Name = "ForestName"; Expression = { $forest.Name } },
                                @{Name = "DomainName"; Expression = { $domain } },
                                @{Name = "DCName"; Expression = { $_.HostName } },
                                @{Name = "SiteName"; Expression = { $_.Site } },
                                IPv4Address,
                                OperatingSystem,
                                IsGlobalCatalog,
                                IsReadOnly
                    } catch {
                        [pscustomobject]@{
                            ForestName = $forest.Name
                            DomainName = $domain
                            DCName = "ERROR"
                            SiteName = $_.Exception.Message
                        }
                    }
                }
