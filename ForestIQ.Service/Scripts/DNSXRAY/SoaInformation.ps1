

Import-Module DnsServer -ErrorAction Stop

$SOAComparison = @()

try {
    # Get all DNS servers hosting this zone
    $Forest = Get-ADForest -ErrorAction Stop
    $DnsServers = @()
    foreach ($Domain in $Forest.Domains) {
        $DCs = Get-ADDomainController -Filter * -Server $Domain -ErrorAction Stop
        foreach ($DC in $DCs) {
            if ($DnsServers -notcontains $DC.HostName) {
                $DnsServers += $DC.HostName
            }
        }
    }

    $SOAInventory = @()

    foreach ($Server in $DnsServers) {
        try {
            # Try to get SOA record for the zone on each server
            $Records = Get-DnsServerResourceRecord -ComputerName $Server -ZoneName $ZoneName -RRType SOA -ErrorAction Stop
            foreach ($Record in $Records) {
                $SOAInventory += [PSCustomObject]@{
                    DnsServer        = $Server
                    ZoneName         = $ZoneName
                    PrimaryServer    = $Record.RecordData.PrimaryServer
                    SerialNumber     = $Record.RecordData.SerialNumber
                    RefreshInterval  = $Record.RecordData.RefreshInterval
                    RetryDelay       = $Record.RecordData.RetryDelay
                    ExpireLimit      = $Record.RecordData.ExpireLimit
                    MinimumTTL       = $Record.RecordData.MinimumTimeToLive.TotalSeconds
                }
            }
        } catch {
            # Server doesn't host this zone or is offline
        }
    }

    $Serials = $SOAInventory | Select-Object -ExpandProperty SerialNumber -Unique
    $Status = "Healthy"
    if ($Serials.Count -gt 1) {
        $Status = "Warning"
    }

    foreach ($Item in $SOAInventory) {
        $Item | Add-Member -MemberType NoteProperty -Name "Status" -Value $Status
        $SOAComparison += $Item
    }

    if ($SOAComparison.Count -eq 0) {
        @() | Write-Output
    } else {
        $SOAComparison | Write-Output
    }
}
catch {
    throw $_
}
