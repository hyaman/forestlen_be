

$ZoneInventory = @()

try {
    # If DnsServer is 'All', we need to fetch all DNS servers first
    $TargetServers = @()
    if ($DnsServer -eq 'All') {
        $Forest = Get-ADForest -ErrorAction Stop
        $Domains = $Forest.Domains
        if ($DomainFilter -ne 'All') {
            $Domains = $Domains | Where-Object { $_ -like "*$DomainFilter*" }
        }

        foreach ($Domain in $Domains) {
            $DCs = Get-ADDomainController -Filter * -Server $Domain -ErrorAction Stop

            if ($SiteFilter -ne 'All') {
                $DCs = $DCs | Where-Object { $_.Site -eq $SiteFilter }
            }
            if ($TargetDC -ne 'All') {
                $DCs = $DCs | Where-Object { $_.HostName -eq $TargetDC -or $_.Name -eq $TargetDC }
            }

            foreach ($DC in $DCs) {
                if ($TargetServers -notcontains $DC.HostName) {
                    $TargetServers += $DC.HostName
                }
            }
        }
    } else {
        $TargetServers += $DnsServer
    }

    foreach ($Server in $TargetServers) {
        try {
            $Zones = Get-DnsServerZone -ComputerName $Server -ErrorAction Stop
            foreach ($Zone in $Zones) {
                $ZoneInventory += [PSCustomObject]@{
                    DnsServer           = $Server
                    ZoneName            = $Zone.ZoneName
                    ZoneType            = $Zone.ZoneType
                    IsDsIntegrated      = $Zone.IsDsIntegrated
                    ReplicationScope    = $Zone.ReplicationScope
                    IsReverseLookupZone = $Zone.IsReverseLookupZone
                    DynamicUpdate       = $Zone.DynamicUpdate
                    SecureSecondaries   = $Zone.SecureSecondaries
                    IsPaused            = $Zone.IsPaused
                    IsShutdown          = $Zone.IsShutdown
                }
            }
        } catch {
            # Silently skip servers we can't query or log
        }
    }
    
    if ($ZoneInventory.Count -eq 0) {
        @() | Write-Output
    } else {
        $ZoneInventory | Write-Output
    }
}
catch {
    throw $_
}
