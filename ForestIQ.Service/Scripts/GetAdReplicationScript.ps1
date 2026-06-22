Import-Module ActiveDirectory -ErrorAction Stop

$report = [pscustomobject][ordered]@{
    GeneratedAt              = Get-Date
    ReplicationPartners      = @()
    ReplicationFailures      = @()
    ReplicationConnections   = @()
    RepadminSummary          = $null
    ReplicationHealthSummary = $null
    Errors                   = @()
    Timings                  = [System.Collections.Generic.List[object]]::new()
}

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

$dcs = @()
try { 
    $forest = Get-ADForest -Credential $cred
    foreach ($domain in $forest.Domains) {
        if ($global:FilterDomain -and ($domain -notmatch $global:FilterDomain)) { continue }
        try {
            $domainDCs = @(Get-ADDomainController -Filter * -Server $domain -Credential $cred -ErrorAction Stop | Select-Object HostName, Site, IPv4Address, Domain)
            if ($global:FilterSite) { $domainDCs = @($domainDCs | Where-Object { $_.Site -match $global:FilterSite }) }
            if ($null -ne $domainDCs -and $domainDCs.Count -gt 0) { $dcs += $domainDCs }
        } catch { }
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'DomainControllers'; Error = $_.Exception.Message }; return $report }

$t0 = Get-Date
try
{
    $replicationReport = [System.Collections.Generic.List[object]]::new()
    $replicationFailures = [System.Collections.Generic.List[object]]::new()
    $seenEdges = @{}

    $dcSiteLookup = @{}
    foreach ($d in $dcs) {
        $short = $d.HostName -replace '\..*',''
        $dcSiteLookup[$short] = $d.Site
        $dcSiteLookup[$d.HostName] = $d.Site
    }

    foreach ($dc in $dcs)
    {
        try
        {
            $replicationData = Get-ADReplicationPartnerMetadata -Target $dc.HostName -Scope Server -Partition * -Credential $cred -ErrorAction Stop
            foreach ($rep in $replicationData)
            {
                $latencyMinutes = if ($rep.LastReplicationSuccess) { [math]::Round(((Get-Date) - $rep.LastReplicationSuccess).TotalMinutes, 0) } else { $null }
                $latencyHours = if ($rep.LastReplicationSuccess) { [math]::Round(((Get-Date) - $rep.LastReplicationSuccess).TotalHours, 2) } else { $null }
                if ($rep.ConsecutiveReplicationFailures -gt 0) { $healthStatus = "FAIL" } elseif ($null -eq $latencyMinutes) { $healthStatus = "UNKNOWN" } elseif ($latencyMinutes -le 15) { $healthStatus = "PASS" } elseif ($latencyMinutes -le 60) { $healthStatus = "WARNING" } elseif ($latencyMinutes -le 240) { $healthStatus = "HIGH WARNING" } else { $healthStatus = "CRITICAL" }
                
                $sourceDCName = "Unknown"
                if ($rep.Partner -match "CN=NTDS Settings,CN=([^,]+)") {
                    $sourceDCName = $Matches[1]
                } else {
                    $sourceDCName = $rep.Partner
                }
                
                $sourceSite = "Unknown"
                if ($dcSiteLookup.ContainsKey($sourceDCName)) {
                    $sourceSite = $dcSiteLookup[$sourceDCName]
                } else {
                    try {
                        $sDc = Get-ADDomainController -Identity $sourceDCName -Credential $cred -ErrorAction Stop
                        $sourceSite = $sDc.Site
                        $dcSiteLookup[$sourceDCName] = $sourceSite
                    } catch {}
                }
                
                $edgeKey = "$sourceDCName-$($dc.HostName)"
                if (-not $seenEdges.ContainsKey($edgeKey)) {
                    $seenEdges[$edgeKey] = $true
                    $replicationReport.Add([pscustomobject]@{
                        Domain                       = $dc.Domain
                        SourceDCName                 = $sourceDCName
                        SourceSite                   = $sourceSite
                        SourceDCFullDN               = $rep.Partner
                        DestinationDC                = $dc.HostName
                        DestinationSite              = $dc.Site
                        IsIntersite                  = ($sourceSite -ne $dc.Site)
                        PartnerType                  = $rep.PartnerType
                        Partition                    = $rep.Partition
                        LastReplicationSuccess       = $rep.LastReplicationSuccess
                        LastReplicationAttempt       = $rep.LastReplicationAttempt
                        LatencyMinutes               = $latencyMinutes
                        ConsecutiveFailures          = $rep.ConsecutiveReplicationFailures
                        LastReplicationResult        = $rep.LastReplicationResult
                        Health                       = $healthStatus
                        
                        SiteName                     = $dc.Site
                        DomainController             = $dc.HostName
                        DCIPAddress                  = $dc.IPv4Address
                        ReplicationPartner           = $rep.Partner
                        NamingContext                = $rep.Partition
                        ReplicationLatencyMinutes    = $latencyMinutes
                        ReplicationLatencyHours      = $latencyHours
                        ReplicationLatencyStatus     = $healthStatus
                        LastReplicationResultMessage = $rep.LastReplicationResultMessage
                        ReplicationStatus            = $healthStatus
                        PartnerStatus                = $healthStatus
                    })
                }
            }
        }
        catch
        {
            $replicationReport.Add([pscustomobject]@{ 
                Domain = $dc.Domain
                DestinationDC = $dc.HostName
                DestinationSite = $dc.Site
                SiteName = $dc.Site
                DomainController = $dc.HostName
                DCIPAddress = $dc.IPv4Address
                SourceDCName = 'Unknown'
                SourceSite = 'Unknown'
                IsIntersite = $false
                SourceDCFullDN = 'Unable to collect replication metadata'
                ReplicationPartner = 'Unable to collect replication metadata'
                PartnerType = $null
                Partition = $null
                NamingContext = $null
                LastReplicationSuccess = $null
                LastReplicationAttempt = $null
                LatencyMinutes = $null
                ReplicationLatencyMinutes = $null
                ReplicationLatencyHours = $null
                ConsecutiveFailures = 0
                LastReplicationResult = -1
                LastReplicationResultMessage = $_.Exception.Message
                Health = 'Error'
                ReplicationLatencyStatus = 'Error'
                ReplicationStatus = 'Error'
                PartnerStatus = 'Error'
            })
        }

        try
        {
            $failures = @(Get-ADReplicationFailure -Target $dc.HostName -Scope Server -Credential $cred -ErrorAction Stop)
            if ($failures.Count -gt 0)
            {
                foreach ($failure in $failures)
                {
                    $status = if ($failure.FailureCount -gt 0) { 'Failure Found' } else { 'Historical Failure' }
                    $replicationFailures.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Server = $failure.Server; Partner = $failure.Partner; FirstFailureTime = $failure.FirstFailureTime; FailureCount = $failure.FailureCount; LastError = $failure.LastError; Status = $status })
                }
            }
            else
            {
                $replicationFailures.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Server = 'None'; Partner = 'None'; FailureCount = 0; LastError = 'No replication failures found'; Status = 'Healthy' })
            }
        }
        catch { $replicationFailures.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Server = 'ERROR'; Partner = 'ERROR'; LastError = $_.Exception.Message; Status = 'Error' }) }
    }

    $report.ReplicationPartners = $replicationReport
    $report.ReplicationFailures = $replicationFailures
}
catch { $report.Errors += [pscustomobject]@{ Section = 'ReplicationPartners'; Error = $_.Exception.Message } }
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
catch { $report.Errors += [pscustomobject]@{ Section = 'ReplicationConnections'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'ReplicationConnections'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

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
            $item = [pscustomobject]@{ Server = $Matches[1]; LargestDelta = $Matches[2]; Failures = [int]$Matches[3]; Total = [int]$Matches[4]; FailurePercent = [int]$Matches[5]; Error = $Matches[6].Trim() }
            if ($currentSection -eq 'Source') { $sourceDSA.Add($item) } elseif ($currentSection -eq 'Destination') { $destinationDSA.Add($item) }
            continue
        }

        if ($currentSection -eq 'Errors' -and $trimmed -match '^(\d+)\s*-\s*(.+)$')
        {
            $operationalErrors.Add([pscustomobject]@{ ErrorCode = [int]$Matches[1]; Server = $Matches[2] })
        }
    }

    $sourceFailures = ($sourceDSA | Measure-Object -Property Failures -Sum).Sum
    $destinationFailures = ($destinationDSA | Measure-Object -Property Failures -Sum).Sum
    $repadminHealthy = ($sourceFailures -eq 0 -and $destinationFailures -eq 0 -and $operationalErrors.Count -eq 0)

    $report.RepadminSummary = [pscustomobject]@{ Healthy = $repadminHealthy; SourceDSA = $sourceDSA; DestinationDSA = $destinationDSA; OperationalErrors = $operationalErrors }

    $activeFailures = 0
    if ($null -ne $report.ReplicationFailures) { $activeFailures = @($report.ReplicationFailures | Where-Object { $_.FailureCount -gt 0 }).Count }
    $report.ReplicationHealthSummary = [pscustomobject]@{ ActiveFailures = $activeFailures; Healthy = ($activeFailures -eq 0 -and $repadminHealthy) }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'RepadminSummary'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'RepadminSummary'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$report
