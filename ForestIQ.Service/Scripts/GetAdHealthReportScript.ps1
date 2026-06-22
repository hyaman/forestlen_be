                Import-Module ActiveDirectory -ErrorAction Stop

                $report = [pscustomobject][ordered]@{
                    GeneratedAt         = Get-Date
                    DomainForestInfo    = $null
                    ForestInfo          = $null
                    Domains             = @()
                    Sites               = @()
                    SiteLinkReport      = @()
                    Subnets             = @()
                    DomainControllers   = @()
                    SiteSubnetDCMapping = @()
                    ReplicationPartners = @()
                    ReplicationFailures = @()
                    ReplicationConnections = @()
                    PortConnectivity    = @()
                    DcLocator           = $null
                    RepadminSummary     = $null
                    ReplicationHealthSummary = $null
                    DcDiagSummary       = $null
                    ConfigurationServers = @()
                    Errors              = @()
                    Timings             = [System.Collections.Generic.List[object]]::new()
                }

                $username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
                $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
                $cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

                $t0 = Get-Date
                try
                {
                    $domain = Get-ADDomain -Credential $cred -ErrorAction Stop
                    $forest = Get-ADForest -Credential $cred -ErrorAction Stop
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'Bootstrap'; Error = $_.Exception.Message }
                    $report
                    return
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'Bootstrap'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $allSites = @()
                $allSubnets = @()

                $t0 = Get-Date
                try
                {
                    $allSites = @(Get-ADReplicationSite -Filter * -Properties Description -Credential $cred -ErrorAction Stop)
                    if ($global:FilterSite) { $allSites = @($allSites | Where-Object { $_.Name -match $global:FilterSite }) }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'Sites'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'Sites'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $allSubnets = @(Get-ADReplicationSubnet -Filter * -Properties Site,Location,Description -Credential $cred -ErrorAction Stop)
                    if ($global:FilterSite) { $allSubnets = @($allSubnets | Where-Object { $_.Site -match $global:FilterSite }) }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'Subnets'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'Subnets'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $userCount = 0
                    $groupCount = 0
                    try { $userCount = @(Get-ADUser -LDAPFilter "(objectClass=user)" -ResultSetSize $null -Credential $cred).Count } catch {}
                    try { $groupCount = @(Get-ADGroup -LDAPFilter "(objectClass=group)" -ResultSetSize $null -Credential $cred).Count } catch {}

                    $report.DomainForestInfo = [pscustomobject]@{
                        DomainName           = $domain.DNSRoot
                        NetBIOSName          = $domain.NetBIOSName
                        DomainMode           = $domain.DomainMode.ToString()
                        ForestName           = $forest.Name
                        ForestMode           = $forest.ForestMode.ToString()
                        UserCount            = $userCount
                        GroupCount           = $groupCount
                        PDCEmulator          = $domain.PDCEmulator
                        RIDMaster            = $domain.RIDMaster
                        InfrastructureMaster = $domain.InfrastructureMaster
                        SchemaMaster         = $forest.SchemaMaster
                        DomainNamingMaster   = $forest.DomainNamingMaster
                    }

                    $report.ForestInfo = [pscustomobject]@{
                        ForestName           = $forest.Name
                        RootDomain           = $forest.RootDomain
                        Domains              = $forest.Domains
                        GlobalCatalogs       = $forest.GlobalCatalogs
                        Sites                = $forest.Sites
                        ForestMode           = $forest.ForestMode.ToString()
                        DomainName           = $domain.DNSRoot
                        NetBIOSName          = $domain.NetBIOSName
                        DomainMode           = $domain.DomainMode.ToString()
                        UserCount            = $userCount
                        GroupCount           = $groupCount
                        PDCEmulator          = $domain.PDCEmulator
                        RIDMaster            = $domain.RIDMaster
                        InfrastructureMaster = $domain.InfrastructureMaster
                        SchemaMaster         = $forest.SchemaMaster
                        DomainNamingMaster   = $forest.DomainNamingMaster
                    }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'DomainForestInfo'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'DomainForestInfo'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $report.Domains = foreach ($domainName in $forest.Domains)
                    {
                        if ($global:FilterDomain -and ($domainName -notmatch $global:FilterDomain)) { continue }
                        try
                        {
                            $domainInfo = Get-ADDomain -Server $domainName -Credential $cred -ErrorAction Stop

                            [pscustomobject]@{
                                DomainName           = $domainInfo.DNSRoot
                                NetBIOSName          = $domainInfo.NetBIOSName
                                DomainMode           = $domainInfo.DomainMode.ToString()
                                ParentDomain         = if ($domainInfo.ParentDomain) { $domainInfo.ParentDomain } else { 'Root Domain' }
                                ChildDomains         = @($domainInfo.ChildDomains)
                                PDCEmulator          = $domainInfo.PDCEmulator
                                RIDMaster            = $domainInfo.RIDMaster
                                InfrastructureMaster = $domainInfo.InfrastructureMaster
                                Status               = 'OK'
                            }
                        }
                        catch
                        {
                            [pscustomobject]@{
                                DomainName = $domainName
                                Status     = 'ERROR'
                                Error      = $_.Exception.Message
                                Exception  = $_.Exception.GetType().FullName
                            }
                        }
                    }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'ForestDiscovery'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'Domains'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $report.Sites = $allSites | Select-Object Name, Description
                $report.Subnets = $allSubnets | Select-Object Name, Site, Location, Description

                $t0 = Get-Date
                try
                {
                    $report.SiteLinkReport =
                        Get-ADReplicationSiteLink -Filter * -Properties Cost,ReplicationFrequencyInMinutes,SitesIncluded,ReplicationSchedule -Credential $cred -ErrorAction Stop |
                        Where-Object { -not $global:FilterSite -or ($_.SitesIncluded -match $global:FilterSite) } |
                        ForEach-Object {
                            [pscustomobject]@{
                                SiteLinkName               = $_.Name
                                Cost                       = $_.Cost
                                ReplicationIntervalMinutes = $_.ReplicationFrequencyInMinutes
                                ScheduleExists             = if ($_.ReplicationSchedule) { 'Yes' } else { 'No' }
                                ScheduleExistsBool         = [bool]$_.ReplicationSchedule
                                SitesIncluded              = ($_.SitesIncluded -join ', ')
                                SitesIncludedArray         = @($_.SitesIncluded)
                                Status = if ($_.ReplicationFrequencyInMinutes -gt 180)
                                {
                                    'Warning'
                                }
                                elseif ($_.ReplicationSchedule)
                                {
                                    'Warning'
                                }
                                elseif ($_.Cost -eq 100 -and $_.ReplicationFrequencyInMinutes -eq 180 -and -not $_.ReplicationSchedule)
                                {
                                    'Informational'
                                }
                                else
                                {
                                    'Healthy'
                                }

                                StatusReason = if ($_.ReplicationFrequencyInMinutes -gt 180)
                                {
                                    "Replication interval is $($_.ReplicationFrequencyInMinutes) minutes, which is higher than the default 180 minutes."
                                }
                                elseif ($_.ReplicationSchedule)
                                {
                                    'A custom replication schedule is configured and should be reviewed.'
                                }
                                elseif ($_.Cost -eq 100 -and $_.ReplicationFrequencyInMinutes -eq 180 -and -not $_.ReplicationSchedule)
                                {
                                    'Site link is using default Active Directory settings.'
                                }
                                else
                                {
                                    'Site link configuration appears healthy.'
                                }
                            }
                        }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'SiteLinkReport'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'SiteLinkReport'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $dcs = @()

                $t0 = Get-Date
                try
                {
                    foreach ($d in $forest.Domains) {
                        if ($global:FilterDomain -and ($d -notmatch $global:FilterDomain)) {
                            continue
                        }
                        try {
                            $domainDCs = @(
                                Get-ADDomainController -Filter * -Server $d -Credential $cred -ErrorAction Stop |
                                Select-Object HostName,
                                              Name,
                                              Site,
                                              IPv4Address,
                                              IsGlobalCatalog,
                                              IsReadOnly,
                                              OperatingSystem,
                                              OperatingSystemVersion,
                                              @{Name = 'UptimeHours'; Expression = {
                                                  $uptime = 0
                                                  try {
                                                      $cim = Get-CimInstance -ClassName Win32_OperatingSystem -ComputerName $_.HostName -ErrorAction Stop
                                                      $uptime = [math]::Round((Get-Date).Subtract($cim.LastBootUpTime).TotalHours, 1)
                                                  } catch {}
                                                  $uptime
                                              }},
                                              OperationMasterRoles
                            )
                            if ($global:FilterSite) {
                                $domainDCs = @($domainDCs | Where-Object { $_.Site -match $global:FilterSite })
                            }
                            if ($null -ne $domainDCs -and $domainDCs.Count -gt 0) { $dcs += $domainDCs }
                        } catch { }
                    }

                    $report.DomainControllers = $dcs
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'DomainControllers'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'DomainControllers'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $configContext = (Get-ADRootDSE -Server $forest.RootDomain -Credential $cred -ErrorAction Stop).configurationNamingContext
                    $allConfigServers = Get-ADObject -Filter "ObjectClass -eq 'server'" -SearchBase $configContext -Server $forest.RootDomain -Properties dNSHostName -Credential $cred -ErrorAction Stop

                    $report.ConfigurationServers = foreach ($srv in $allConfigServers)
                    {
                        $siteName = $null
                        if ($srv.DistinguishedName -match "CN=Servers,CN=([^,]+),CN=Sites") {
                            $siteName = $Matches[1]
                        }
                        if ($global:FilterSite -and ($siteName -notmatch $global:FilterSite)) { continue }

                        [pscustomobject]@{
                            Name     = $srv.Name
                            HostName = $srv.dNSHostName
                            Site     = $siteName
                        }
                    }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'ConfigurationServers'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'ConfigurationServers'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $report.SiteSubnetDCMapping = foreach ($site in $allSites)
                    {
                        $siteSubnets = @($allSubnets | Where-Object { $_.Site -like "*CN=$($site.Name),*" -or $_.Site -eq $site.Name })
                        $siteDCs = @($dcs | Where-Object { $_.Site -eq $site.Name })
                        $subnetNames = @($siteSubnets | Select-Object -ExpandProperty Name)

                        if ($siteDCs)
                        {
                            foreach ($dc in $siteDCs)
                            {
                                [pscustomobject]@{
                                    SiteName         = $site.Name
                                    Subnets          = if ($subnetNames.Count -gt 0) { ($subnetNames -join ', ') } else { 'No subnet assigned' }
                                    SubnetList       = $subnetNames
                                    DomainController = $dc.HostName
                                    DCName           = $dc.Name
                                    DCIPAddress      = $dc.IPv4Address
                                    IsGlobalCatalog  = $dc.IsGlobalCatalog
                                    IsReadOnly       = $dc.IsReadOnly
                                    OperatingSystem  = $dc.OperatingSystem
                                    FSMORoles        = if ($dc.OperationMasterRoles) { ($dc.OperationMasterRoles -join ', ') } else { 'None' }
                                }
                            }
                        }
                        else
                        {
                            [pscustomobject]@{
                                SiteName         = $site.Name
                                Subnets          = if ($subnetNames.Count -gt 0) { ($subnetNames -join ', ') } else { 'No subnet assigned' }
                                SubnetList       = $subnetNames
                                DomainController = $null
                                DCName           = $null
                                DCIPAddress      = $null
                                IsGlobalCatalog  = $null
                                IsReadOnly       = $null
                                OperatingSystem  = $null
                                FSMORoles        = 'None'
                            }
                        }
                    }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'SiteSubnetDCMapping'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'SiteSubnetDCMapping'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                function Invoke-FirstSuccessfulAdCommand
                {
                    param(
                        [scriptblock] $Command,
                        [string[]] $ServersToTry
                    )

                    $errors = [System.Collections.Generic.List[string]]::new()

                    foreach ($server in @($ServersToTry | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique))
                    {
                        try
                        {
                            return & $Command $server
                        }
                        catch
                        {
                            $errors.Add("${server}: $($_.Exception.Message)")
                        }
                    }

                    throw [System.Exception]::new(($errors -join ' | '))
                }

                $t0 = Get-Date
                try
                {
                    $replicationReport = [System.Collections.Generic.List[object]]::new()
                    $replicationFailures = [System.Collections.Generic.List[object]]::new()

                    foreach ($dc in $dcs)
                    {
                        try
                        {
                            $replicationData = Get-ADReplicationPartnerMetadata `
                                 -Target $dc.HostName `
                                 -Scope Server `
                                 -Credential $cred `
                                 -ErrorAction Stop

                            foreach ($rep in $replicationData)
                            {
                                $latencyMinutes = if ($rep.LastReplicationSuccess) { [math]::Round(((Get-Date) - $rep.LastReplicationSuccess).TotalMinutes, 0) } else { $null }
                                $latencyHours = if ($rep.LastReplicationSuccess) { [math]::Round(((Get-Date) - $rep.LastReplicationSuccess).TotalHours, 2) } else { $null }

                                if ($rep.ConsecutiveReplicationFailures -gt 0)
                                {
                                    $healthStatus = "FAIL"
                                }
                                elseif ($null -eq $latencyMinutes)
                                {
                                    $healthStatus = "UNKNOWN"
                                }
                                elseif ($latencyMinutes -le 15)
                                {
                                    $healthStatus = "PASS"
                                }
                                elseif ($latencyMinutes -le 60)
                                {
                                    $healthStatus = "WARNING"
                                }
                                elseif ($latencyMinutes -le 240)
                                {
                                    $healthStatus = "HIGH WARNING"
                                }
                                else
                                {
                                    $healthStatus = "CRITICAL"
                                }

                                $sourceDCName = "Unknown"
                                if ($rep.Partner -match "CN=NTDS Settings,CN=([^,]+)")
                                {
                                    $sourceDCName = $Matches[1]
                                }
                                else
                                {
                                    $sourceDCName = $rep.Partner
                                }
                                
                                $replicationReport.Add([pscustomobject]@{
                                    SiteName                     = $dc.Site
                                    DomainController             = $dc.HostName
                                    DCIPAddress                  = $dc.IPv4Address
                                    SourceDCName                 = $sourceDCName
                                    ReplicationPartner           = $rep.Partner
                                    NamingContext                = $rep.Partition
                                    LastReplicationAttempt       = $rep.LastReplicationAttempt
                                    LastReplicationSuccess       = $rep.LastReplicationSuccess
                                    ReplicationLatencyMinutes    = $latencyMinutes
                                    ReplicationLatencyHours      = $latencyHours
                                    ReplicationLatencyStatus     = $healthStatus
                                    ConsecutiveFailures          = $rep.ConsecutiveReplicationFailures
                                    LastReplicationResult        = $rep.LastReplicationResult
                                    LastReplicationResultMessage = $rep.LastReplicationResultMessage
                                    LastReplicationResultMsg     = $rep.LastReplicationResultMessage
                                    ReplicationStatus            = $healthStatus
                                    PartnerStatus                = $healthStatus
                                })
                            }
                        }
                        catch
                        {
                            $replicationReport.Add([pscustomobject]@{
                                SiteName                     = $dc.Site
                                DomainController             = $dc.HostName
                                DCIPAddress                  = $dc.IPv4Address
                                ReplicationPartner           = 'Unable to collect replication metadata'
                                NamingContext                = $null
                                LastReplicationAttempt       = $null
                                LastReplicationSuccess       = $null
                                ReplicationLatencyMinutes    = $null
                                ReplicationLatencyHours      = $null
                                ReplicationLatencyStatus     = 'Error'
                                ConsecutiveFailures          = $null
                                LastReplicationResult        = $null
                                LastReplicationResultMessage = $_.Exception.Message
                                LastReplicationResultMsg     = $_.Exception.Message
                                ReplicationStatus            = 'Error'
                                PartnerStatus                = 'Error'
                            })
                        }

                        try
                        {
                            $failures = @(Get-ADReplicationFailure `
                                -Target $dc.HostName `
                                -Scope Server `
                                -Credential $cred `
                                -ErrorAction Stop)

                            if ($failures.Count -gt 0)
                            {
                                foreach ($failure in $failures)
                                {
                                    $status = if ($failure.FailureCount -gt 0) { 'Failure Found' } else { 'Historical Failure' }
                                    $replicationFailures.Add([pscustomobject]@{
                                        DomainController = $dc.HostName
                                        SiteName         = $dc.Site
                                        Server           = $failure.Server
                                        Partner          = $failure.Partner
                                        FirstFailureTime = $failure.FirstFailureTime
                                        FailureCount     = $failure.FailureCount
                                        LastError        = $failure.LastError
                                        Status           = $status
                                    })
                                }
                            }
                            else
                            {
                                $replicationFailures.Add([pscustomobject]@{
                                    DomainController = $dc.HostName
                                    SiteName         = $dc.Site
                                    Server           = 'None'
                                    Partner          = 'None'
                                    FirstFailureTime = $null
                                    FailureCount     = 0
                                    LastError        = 'No replication failures found'
                                    Status           = 'Healthy'
                                })
                            }
                        }
                        catch
                        {
                            $replicationFailures.Add([pscustomobject]@{
                                DomainController = $dc.HostName
                                SiteName         = $dc.Site
                                Server           = 'ERROR'
                                Partner          = 'ERROR'
                                FirstFailureTime = $null
                                FailureCount     = $null
                                LastError        = $_.Exception.Message
                                Status           = 'Error'
                            })
                        }
                    }

                    $report.ReplicationPartners = $replicationReport
                    $report.ReplicationFailures = $replicationFailures
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'ReplicationPartners'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'ReplicationPartners'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $replicationConnections = [System.Collections.Generic.List[object]]::new()
                    $connections = @(Get-ADReplicationConnection -Filter * -Properties * -Credential $cred -ErrorAction Stop)

                    foreach ($conn in $connections)
                    {
                        $sourceDC = "Unknown"
                        $sourceSite = "Unknown"
                        $targetDC = "Unknown"
                        $targetSite = "Unknown"

                        if ($conn.ReplicateFromDirectoryServer -match "CN=NTDS Settings,CN=([^,]+),CN=Servers,CN=([^,]+)") {
                            $sourceDC = $Matches[1]
                            $sourceSite = $Matches[2]
                        }

                        if ($conn.ReplicateToDirectoryServer -match "CN=NTDS Settings,CN=([^,]+),CN=Servers,CN=([^,]+)") {
                            $targetDC = $Matches[1]
                            $targetSite = $Matches[2]
                        } elseif ($conn.DistinguishedName -match "CN=([^,]+),CN=NTDS Settings,CN=([^,]+),CN=Servers,CN=([^,]+)") {
                            $targetDC = $Matches[2]
                            $targetSite = $Matches[3]
                        }

                        $connectionType = "IntraSite"
                        if ($conn.InterSiteTransportProtocol) {
                            $connectionType = "InterSite"
                        }

                        $transportProtocol = "RPC"
                        if ($conn.InterSiteTransportProtocol -match "CN=([^,]+),") {
                            $transportProtocol = $Matches[1]
                        } elseif ($conn.InterSiteTransportProtocol) {
                            $transportProtocol = $conn.InterSiteTransportProtocol
                        }

                        $replicationConnections.Add([pscustomobject]@{
                            ConnectionName                    = $conn.Name
                            SourceDC                          = $sourceDC
                            SourceSite                        = $sourceSite
                            TargetDC                          = $targetDC
                            TargetSite                        = $targetSite
                            ConnectionType                    = $connectionType
                            TransportProtocol                 = $transportProtocol
                            EnabledConnection                 = $conn.Enabled
                            Options                           = $conn.Options
                            ReplicatedNamingContexts          = $conn.ReplicatedNamingContexts
                            PartiallyReplicatedNamingContexts = $conn.PartiallyReplicatedNamingContexts
                            ReplicationReasons                = $conn.'mS-DS-ReplicatesNCReason'
                            DistinguishedName                 = $conn.DistinguishedName
                            Created                           = $conn.Created
                            Modified                          = $conn.Modified
                        })
                    }

                    $report.ReplicationConnections = $replicationConnections
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'ReplicationConnections'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'ReplicationConnections'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $ADPorts = [ordered]@{
                        DNS              = 53
                        Kerberos         = 88
                        RPC              = 135
                        LDAP             = 389
                        SMB              = 445
                        KerberosPassword = 464
                        LDAPS            = 636
                        GlobalCatalog    = 3268
                        GlobalCatalogSSL = 3269
                        ADWS             = 9389
                        WinRM            = 5985
                    }

                    $TimeoutMs = 1000
                    $tasks = [System.Collections.Generic.List[hashtable]]::new()

                    foreach ($dc in $dcs)
                    {
                        foreach ($port in $ADPorts.GetEnumerator())
                        {
                            $tcp = [System.Net.Sockets.TcpClient]::new()
                            $task = $tcp.ConnectAsync($dc.HostName, $port.Value)

                            $tasks.Add(@{
                                Tcp              = $tcp
                                Task             = $task
                                DomainController = $dc.HostName
                                SiteName         = $dc.Site
                                Service          = $port.Key
                                Port             = $port.Value
                            })
                        }
                    }

                    [System.Threading.Tasks.Task[]]$allTasks = $tasks | ForEach-Object { $_.Task }
                    $portResults = [System.Collections.Generic.List[object]]::new()

                    try
                    {
                        $deadline = [System.Threading.Tasks.Task]::WhenAll($allTasks)
                        [void]$deadline.Wait($TimeoutMs)

                        foreach ($t in $tasks)
                        {
                            $open = $t.Task.Status -eq [System.Threading.Tasks.TaskStatus]::RanToCompletion -and $t.Tcp.Connected

                            $portResults.Add([pscustomobject]@{
                                DomainController = $t.DomainController
                                SiteName         = $t.SiteName
                                Service          = $t.Service
                                Port             = $t.Port
                                Open             = $open
                            })
                        }
                    }
                    finally
                    {
                        foreach ($t in $tasks)
                        {
                            try { $t.Tcp.Close() } catch {}
                        }
                    }

                    $report.PortConnectivity = $portResults | Sort-Object DomainController, Port
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'PortConnectivity'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'PortConnectivity'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $raw = nltest /dsgetdc:$($domain.DNSRoot)
                    $dcLocator = [ordered]@{}

                    foreach ($line in $raw)
                    {
                        switch -Regex ($line)
                        {
                            '^DC:\s+(.*)$'            { $dcLocator.DomainController = $Matches[1].Trim('\') }
                            '^Address:\s+(.*)$'       { $dcLocator.Address = $Matches[1].Trim('\') }
                            '^Dom Name:\s+(.*)$'      { $dcLocator.DomainName = $Matches[1] }
                            '^Forest Name:\s+(.*)$'   { $dcLocator.ForestName = $Matches[1] }
                            '^Dc Site Name:\s+(.*)$'  { $dcLocator.DcSiteName = $Matches[1] }
                            '^Our Site Name:\s+(.*)$' { $dcLocator.OurSiteName = $Matches[1] }
                            '^Flags:\s+(.*)$'         { $dcLocator.Flags = $Matches[1] }
                        }
                    }

                    $report.DcLocator = [pscustomobject]$dcLocator
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'DcLocator'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'DcLocator'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                $t0 = Get-Date
                try
                {
                    $authArgsUser = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
                    $authArgsPass = $global:RemotePassword

                    $raw = cmd /c "repadmin /replsummary /u:$authArgsUser /pw:$authArgsPass 2>&1"
                    $sourceDSA = [System.Collections.Generic.List[object]]::new()
                    $destinationDSA = [System.Collections.Generic.List[object]]::new()
                    $operationalErrors = [System.Collections.Generic.List[object]]::new()
                    $currentSection = ''

                    foreach ($line in $raw)
                    {
                        $trimmed = $line.Trim()

                        if ($trimmed -match '^Source DSA') { $currentSection = 'Source'; continue }
                        if ($trimmed -match '^Destination DSA') { $currentSection = 'Destination'; continue }
                        if ($trimmed -match '^Experienced the following operational errors') { $currentSection = 'Errors'; continue }
                        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }

                        if ($trimmed -match '^(\S+)\s+(\S+)\s+(\d+)\s*/\s*(\d+)\s+(\d+)\s*(.*)$')
                        {
                            $item = [pscustomobject]@{
                                Server         = $Matches[1]
                                LargestDelta   = $Matches[2]
                                Failures       = [int]$Matches[3]
                                Total          = [int]$Matches[4]
                                FailurePercent = [int]$Matches[5]
                                Error          = $Matches[6].Trim()
                            }

                            if ($currentSection -eq 'Source') { $sourceDSA.Add($item) }
                            elseif ($currentSection -eq 'Destination') { $destinationDSA.Add($item) }

                            continue
                        }

                        if ($currentSection -eq 'Errors' -and $trimmed -match '^(\d+)\s*-\s*(.+)$')
                        {
                            $operationalErrors.Add([pscustomobject]@{
                                ErrorCode = [int]$Matches[1]
                                Server    = $Matches[2]
                            })
                        }
                    }

                    $sourceFailures = ($sourceDSA | Measure-Object -Property Failures -Sum).Sum
                    $destinationFailures = ($destinationDSA | Measure-Object -Property Failures -Sum).Sum

                    $repadminHealthy = ($sourceFailures -eq 0 -and $destinationFailures -eq 0 -and $operationalErrors.Count -eq 0)

                    $report.RepadminSummary = [pscustomobject]@{
                        Healthy = $repadminHealthy
                        SourceDSA = $sourceDSA
                        DestinationDSA = $destinationDSA
                        OperationalErrors = $operationalErrors
                    }

                    $activeFailures = 0
                    if ($null -ne $report.ReplicationFailures) {
                        $activeFailures = @($report.ReplicationFailures | Where-Object { $_.FailureCount -gt 0 }).Count
                    }

                    $report.ReplicationHealthSummary = [pscustomobject]@{
                        ActiveFailures = $activeFailures
                        Healthy = ($activeFailures -eq 0 -and $repadminHealthy)
                    }
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{ Section = 'RepadminSummary'; Error = $_.Exception.Message }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'RepadminSummary'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })


                function Get-DcDiagIssue
                {
                    param(
                        [string]$Output
                    )
                
                    switch -Regex ($Output)
                    {
                        '(?i)Directory Binding Error 5|Access is denied'
                        {
                            return @{
                                Status   = 'Error'
                                Category = 'Permissions'
                                Details  = 'Access denied while querying the Domain Controller.'
                            }
                        }
                
                        '(?i)Record registrations cannot be found'
                        {
                            return @{
                                Status   = 'Warning'
                                Category = 'DNS'
                                Details  = 'Domain Controller DNS records are missing or not registered.'
                            }
                        }
                
                        '(?i)forwarders.*invalid'
                        {
                            return @{
                                Status   = 'Warning'
                                Category = 'DNS'
                                Details  = 'DNS forwarders are invalid or unreachable.'
                            }
                        }
                
                        '(?i)failed test DNS'
                        {
                            return @{
                                Status   = 'Warning'
                                Category = 'DNS'
                                Details  = 'DNS health test failed.'
                            }
                        }
                
                        '(?i)failed test Replications'
                        {
                            return @{
                                Status   = 'Critical'
                                Category = 'Replication'
                                Details  = 'Active Directory replication failures detected.'
                            }
                        }
                
                        '(?i)RPC server is unavailable'
                        {
                            return @{
                                Status   = 'Error'
                                Category = 'Connectivity'
                                Details  = 'RPC communication with the Domain Controller failed.'
                            }
                        }
                
                        '(?i)Unable to contact the server'
                        {
                            return @{
                                Status   = 'Error'
                                Category = 'Connectivity'
                                Details  = 'Domain Controller is unreachable.'
                            }
                        }
                
                        '(?i)SysVolCheck'
                        {
                            return @{
                                Status   = 'Critical'
                                Category = 'SYSVOL'
                                Details  = 'SYSVOL replication is unhealthy.'
                            }
                        }
                
                        '(?i)NetLogons'
                        {
                            return @{
                                Status   = 'Critical'
                                Category = 'NetLogon'
                                Details  = 'NetLogon share is unavailable.'
                            }
                        }
                
                        default
                        {
                            return @{
                                Status   = 'Issues Found'
                                Category = 'Other'
                                Details  = $Output
                            }
                        }
                    }
                }

                $t0 = Get-Date
                try
                {
                    $perDcDiag = [System.Collections.Generic.List[object]]::new()
                    
                    $dcdiagUser = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
                    $dcdiagPass = $global:RemotePassword

                    $runningProcs = @()
                    foreach ($dc in $dcs)
                    {
                        $dcdiagArgs = @("/s:$($dc.HostName)", "/test:Advertising", "/test:Services", "/test:DNS", "/DnsBasic", "/DnsRecordRegistration", "/q")
                        if ($dcdiagUser) {
                            $dcdiagArgs += "/u:$dcdiagUser"
                            $dcdiagArgs += "/p:$dcdiagPass"
                        }

                        $guid = [guid]::NewGuid().ToString()
                        $outPath = "$env:TEMP\dcdiag_$guid.log"
                        $errPath = "$env:TEMP\dcdiag_err_$guid.log"

                        $p = Start-Process -FilePath "dcdiag.exe" -ArgumentList $dcdiagArgs -NoNewWindow -PassThru -RedirectStandardOutput $outPath -RedirectStandardError $errPath
                        $runningProcs += [pscustomobject]@{
                            DC = $dc
                            Process = $p
                            OutPath = $outPath
                            ErrPath = $errPath
                        }
                    }

                    foreach ($item in $runningProcs)
                    {
                        $dc = $item.DC
                        $p = $item.Process
                        
                        try
                        {
                            $exited = $p.WaitForExit(120000)
                            
                            if (-not $exited) {
                                $p.Kill()
                            }

                            $stdout = if (Test-Path $item.OutPath) { Get-Content $item.OutPath -Raw } else { "" }
                            $stderr = if (Test-Path $item.ErrPath) { Get-Content $item.ErrPath -Raw } else { "" }
                            
                            Remove-Item $item.OutPath -ErrorAction SilentlyContinue
                            Remove-Item $item.ErrPath -ErrorAction SilentlyContinue

                            $outputString = "$stdout`n$stderr".Trim()

                            if (-not $exited) {
                                $perDcDiag.Add([pscustomobject]@{
                                    DomainController = $dc.HostName
                                    SiteName         = $dc.Site
                                    Status           = "Error"
                                    Category         = "Timeout"
                                    Details          = "Job timed out after 120 seconds. Review RawOutput for partial logs."
                                    RawOutput        = $outputString
                                })
                            }
                            elseif ([string]::IsNullOrWhiteSpace($outputString) -or $outputString -match "passed test") {
                                $perDcDiag.Add([pscustomobject]@{
                                    DomainController = $dc.HostName
                                    SiteName         = $dc.Site
                                    Status           = "Healthy"
                                    Category         = "None"
                                    Details          = "No issues detected."
                                    RawOutput        = $outputString
                                })
                            }
                            else {
                                $issue = Get-DcDiagIssue $outputString
                
                                $perDcDiag.Add([pscustomobject]@{
                                    DomainController = $dc.HostName
                                    SiteName         = $dc.Site
                                    Status           = $issue.Status
                                    Category         = $issue.Category
                                    Details          = $issue.Details
                                    RawOutput        = $outputString
                                })
                            }
                        }
                        catch
                        {
                            $perDcDiag.Add([pscustomobject]@{
                                DomainController = $dc.HostName
                                SiteName         = $dc.Site
                                Status           = "Error"
                                Details          = $_.Exception.Message
                                RawOutput        = $null
                            })
                        }
                    }

                    $report.DcDiagSummary = $perDcDiag
                }
                catch
                {
                    $report.Errors += [pscustomobject]@{
                        Section = 'DcDiagSummary'
                        Error   = $_.Exception.Message
                    }
                }
                $report.Timings.Add([pscustomobject]@{ Section = 'DcDiagSummary'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

                

                $report
                
