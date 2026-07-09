Import-Module ActiveDirectory -ErrorAction Stop

$ReplicationHealth = @()
$DnsPartitionReplication = @()
$seenEdges = @{}

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$DomainControllers = @()
$ADParams = @{}
if ($Credential) {
    $ADParams.Credential = $Credential
    if ($global:RemoteDomain) {
        $ADParams.Server = $global:RemoteDomain
    }
}
$Forest = Get-ADForest @ADParams -ErrorAction SilentlyContinue
if ($Forest) {
    $Domains = $Forest.Domains
    if (-not [string]::IsNullOrWhiteSpace($DomainFilter) -and $DomainFilter -ne 'All') {
        $Domains = $Domains | Where-Object { $_ -like "*$DomainFilter*" }
    }
    foreach ($Domain in $Domains) {
        if ($Credential) {
            $DCs = @(Get-ADDomainController -Filter * -Server $Domain -Credential $Credential -ErrorAction SilentlyContinue)
        }
        else {
            $DCs = @(Get-ADDomainController -Filter * -Server $Domain -ErrorAction SilentlyContinue)
        }
        if (-not [string]::IsNullOrWhiteSpace($SiteFilter) -and $SiteFilter -ne 'All') {
            $DCs = $DCs | Where-Object { $_.Site -eq $SiteFilter }
        }
        if (-not [string]::IsNullOrWhiteSpace($TargetDC) -and $TargetDC -ne 'All') {
            $DCs = $DCs | Where-Object { $_.HostName -eq $TargetDC -or $_.Name -eq $TargetDC }
        }
        if (-not [string]::IsNullOrWhiteSpace($DnsServer) -and $DnsServer -ne 'All') {
            $DCs = $DCs | Where-Object { $_.HostName -eq $DnsServer -or $_.Name -eq $DnsServer }
        }
        $DomainControllers += $DCs
    }
}
$DomainControllers = $DomainControllers | Select-Object -Unique -Property HostName

foreach ($DC in $DomainControllers) {
    try {
        if ($Credential) {
            $Partners = Get-ADReplicationPartnerMetadata -Target $DC.HostName -Scope Server -Partition * -Credential $Credential -ErrorAction Stop
        }
        else {
            $Partners = Get-ADReplicationPartnerMetadata -Target $DC.HostName -Scope Server -Partition * -ErrorAction Stop
        }

        foreach ($Partner in $Partners) {
            $LastSuccess = $Partner.LastReplicationSuccess
            $LastAttempt = $Partner.LastReplicationAttempt
            $MinutesSinceSuccess = $null

            if ($LastSuccess) {
                $MinutesSinceSuccess = [math]::Round(((Get-Date) - $LastSuccess).TotalMinutes, 2)
            }

            $Status = "Healthy"

            if ($Partner.ConsecutiveReplicationFailures -gt 0) {
                $Status = "Critical"
            }
            elseif ($LastSuccess -and ((Get-Date) - $LastSuccess).TotalMinutes -gt 60) {
                $Status = "Warning"
            }

            # Format Partition
            $PartitionRaw = $Partner.Partition
            $PartitionFormatted = $PartitionRaw
            if ($PartitionRaw -like "*DomainDnsZones*") { $PartitionFormatted = "DomainDnsZones" }
            elseif ($PartitionRaw -like "*ForestDnsZones*") { $PartitionFormatted = "ForestDnsZones" }
            elseif ($PartitionRaw -like "*Configuration*") { $PartitionFormatted = "Configuration" }
            elseif ($PartitionRaw -like "*Schema*") { $PartitionFormatted = "Schema" }
            else { $PartitionFormatted = "Domain" }

            # Extract Target Short Name
            $TargetShort = $DC.HostName.Split('.')[0]

            # Extract Source Short Name
            $SourcePartnerRaw = $Partner.Partner
            $SourceShort = $SourcePartnerRaw
            if ($SourcePartnerRaw -match "CN=NTDS Settings,CN=([^,]+),") {
                $SourceShort = $matches[1]
            } elseif ($SourcePartnerRaw -match "^CN=([^,]+),") {
                $SourceShort = $matches[1]
            }

            $Obj = [PSCustomObject]@{
                Source              = $SourceShort
                Target              = $TargetShort
                DestinationDNS      = $DC.HostName
                DestinationSite     = $DC.Site
                SourcePartner       = $Partner.Partner
                Partition           = $PartitionFormatted
                LastAttempt         = $LastAttempt
                LastSuccess         = $LastSuccess
                MinutesSinceSuccess = $MinutesSinceSuccess
                ConsecutiveFailures = $Partner.ConsecutiveReplicationFailures
                LastResult          = $Partner.LastReplicationResult
                ScheduledSync       = $Partner.ScheduledSync
                SyncOnStartup       = $Partner.SyncOnStartup
                TwoWaySync          = $Partner.TwoWaySync
                PartnerType         = $Partner.PartnerType
                Status              = $Status
            }

            if ($Partner.Partition -like "*DomainDnsZones*" -or $Partner.Partition -like "*ForestDnsZones*") {
                $DnsPartitionReplication += $Obj
            }

            $edgeKey = "$($Partner.Partner)-$($DC.HostName)"
            if (-not $seenEdges.ContainsKey($edgeKey)) {
                $seenEdges[$edgeKey] = $true
                $ReplicationHealth += $Obj
            }
        }
    }
    catch {
        $ReplicationHealth += [PSCustomObject]@{
            Source              = "Unknown"
            Target              = $DC.HostName.Split('.')[0]
            DestinationDNS      = $DC.HostName
            DestinationSite     = $DC.Site
            SourcePartner       = "Unknown"
            Partition           = "Unknown"
            LastAttempt         = $null
            LastSuccess         = $null
            MinutesSinceSuccess = $null
            ConsecutiveFailures = 0
            LastResult          = -1
            ScheduledSync       = $false
            SyncOnStartup       = $false
            TwoWaySync          = $false
            PartnerType         = $null
            Status              = "Critical - $($_.Exception.Message)"
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($HealthFilter) -and $HealthFilter -ne 'All') {
    $ReplicationHealth = $ReplicationHealth | Where-Object { $_.Status -like "*$HealthFilter*" }
    $DnsPartitionReplication = $DnsPartitionReplication | Where-Object { $_.Status -like "*$HealthFilter*" }
}

[PSCustomObject]@{
    ReplicationHealth       = $ReplicationHealth
    DnsPartitionReplication = $DnsPartitionReplication
} | Write-Output
