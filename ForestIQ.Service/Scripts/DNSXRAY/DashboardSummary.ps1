Import-Module DnsServer -ErrorAction SilentlyContinue

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$TotalDnsServers = 0
$ReachableDnsServers = 0
$TotalZones = 0
$AdIntegratedZones = 0
$ReverseZones = 0
$TotalRecords = 0
$WarningItems = 0
$CriticalItems = 0

# 1. DNS SERVERS
try {
    $ADParams = @{}
    if ($Credential) {
        $ADParams.Credential = $Credential
        if ($global:RemoteDomain) {
            $ADParams.Server = $global:RemoteDomain
        }
    }
    
    $Forest = Get-ADForest @ADParams -ErrorAction SilentlyContinue
    if ($Forest) {
        $AllDCs = @()
        foreach ($Domain in $Forest.Domains) {
            $DomainADParams = $ADParams.Clone()
            $DomainADParams.Server = $Domain
            $DCs = @(Get-ADDomainController -Filter * @DomainADParams -ErrorAction SilentlyContinue)
            $AllDCs += $DCs
        }
        $TotalDnsServers = $AllDCs.Count
        $ReachableDnsServers = $AllDCs.Count 
    }
}
catch {}

# 2. DNS ZONES & RECORDS & BEST PRACTICES
$InvokeErrors = $null
$remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ArgumentList $ZoneName -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock {
    param($ZoneName)
    $LocalSummary = [PSCustomObject]@{
        TotalZones = 0
        AdIntegratedZones = 0
        ReverseZones = 0
        TotalRecords = 0
        WarningItems = 0
        CriticalItems = 0
    }
    try {
        if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
            $Zones = @(Get-DnsServerZone -ErrorAction SilentlyContinue)
        }
        else {
            $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction SilentlyContinue)
        }

        $LocalSummary.TotalZones = $Zones.Count
        
        foreach ($Zone in $Zones) {
            if ($Zone.IsDsIntegrated) { $LocalSummary.AdIntegratedZones++ }
            if ($Zone.IsReverseLookupZone) { $LocalSummary.ReverseZones++ }

            # Zone Best Practices
            if ($Zone.ZoneType -eq "Primary" -and $Zone.IsDsIntegrated -ne $true) { $LocalSummary.WarningItems++ }
            if ($Zone.ZoneType -eq "Primary" -and $Zone.IsDsIntegrated -eq $true -and $Zone.IsReverseLookupZone -ne $true -and $Zone.DynamicUpdate -ne "Secure") { $LocalSummary.WarningItems++ }
            if ($Zone.IsDsIntegrated -eq $true -and [string]::IsNullOrWhiteSpace($Zone.ReplicationScope)) { $LocalSummary.WarningItems++ }
            if ($Zone.IsPaused -eq $true -or $Zone.IsShutdown -eq $true) { $LocalSummary.CriticalItems++ }

            # Records
            $Records = @(Get-DnsServerResourceRecord -ZoneName $Zone.ZoneName -ErrorAction SilentlyContinue)
            $LocalSummary.TotalRecords += $Records.Count
        }
    }
    catch {}

    # Server Best Practices
    try {
        $Forwarders = Get-DnsServerForwarder -ErrorAction SilentlyContinue
        if (!$Forwarders.IPAddress -or $Forwarders.IPAddress.Count -eq 0) { $LocalSummary.WarningItems++ }

        $Scavenging = Get-DnsServerScavenging -ErrorAction SilentlyContinue
        if ($Scavenging.ScavengingState -ne $true) { $LocalSummary.WarningItems++ }

        $Recursion = Get-DnsServerRecursion -ErrorAction SilentlyContinue
        if ($Recursion.Enable -ne $true) { $LocalSummary.WarningItems++ }
    }
    catch {}

    return $LocalSummary
}

if ($remoteResults) {
    $TotalZones        = $remoteResults.TotalZones
    $AdIntegratedZones = $remoteResults.AdIntegratedZones
    $ReverseZones      = $remoteResults.ReverseZones
    $TotalRecords      = $remoteResults.TotalRecords
    $WarningItems      = $remoteResults.WarningItems
    $CriticalItems     = $remoteResults.CriticalItems
}

# Health Score
$HealthScore = 100 - ($WarningItems * 5) - ($CriticalItems * 20)
if ($HealthScore -lt 0) { $HealthScore = 0 }

$HealthStatus = "Healthy"
if ($HealthScore -lt 70) { $HealthStatus = "Critical" }
elseif ($HealthScore -lt 90) { $HealthStatus = "Warning" }

[PSCustomObject]@{
    TotalDnsServers     = $TotalDnsServers
    ReachableDnsServers = $ReachableDnsServers
    TotalZones          = $TotalZones
    AdIntegratedZones   = $AdIntegratedZones
    ReverseZones        = $ReverseZones
    TotalRecords        = $TotalRecords
    WarningItems        = $WarningItems
    CriticalItems       = $CriticalItems
    HealthScore         = $HealthScore
    HealthStatus        = $HealthStatus
} | Write-Output
