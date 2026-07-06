Import-Module DnsServer -ErrorAction Stop

$DuplicateResults = @()

try {
    $Records = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -ErrorAction Stop

    $DnsInventory = @()
    foreach ($Record in $Records) {
        $RecordData = Get-RecordDataValue -Record $Record
        if ($Record.HostName -eq "@") { $FQDN = $ZoneName } else { $FQDN = "$($Record.HostName).$($ZoneName)" }

        $DnsInventory += [PSCustomObject]@{
            DnsServer  = $DnsServer
            ZoneName   = $ZoneName
            FQDN       = $FQDN
            RecordType = $Record.RecordType
            RecordData = $RecordData
        }
    }

    $DuplicateNames = $DnsInventory |
        Where-Object { $_.RecordType -notin @("SOA", "NS") } |
        Group-Object FQDN, RecordType |
        Where-Object { $_.Count -gt 1 }

    foreach ($Group in $DuplicateNames) {
        foreach ($Item in $Group.Group) {
            $DuplicateResults += [PSCustomObject]@{
                DuplicateType  = "Duplicate Name and Type"
                DnsServer      = $Item.DnsServer
                ZoneName       = $Item.ZoneName
                FQDN           = $Item.FQDN
                RecordType     = $Item.RecordType
                RecordData     = $Item.RecordData
                Count          = $Group.Count
                Recommendation = "Review whether this is expected, such as load balancing. Remove unwanted duplicates."
            }
        }
    }

    $DuplicateIPs = $DnsInventory |
        Where-Object { $_.RecordType -eq "A" -and $_.RecordData -match "^\d{1,3}(\.\d{1,3}){3}$" } |
        Group-Object RecordData |
        Where-Object { $_.Count -gt 1 }

    foreach ($Group in $DuplicateIPs) {
        foreach ($Item in $Group.Group) {
            $DuplicateResults += [PSCustomObject]@{
                DuplicateType  = "Duplicate IP Address"
                DnsServer      = $Item.DnsServer
                ZoneName       = $Item.ZoneName
                FQDN           = $Item.FQDN
                RecordType     = $Item.RecordType
                RecordData     = $Item.RecordData
                Count          = $Group.Count
                Recommendation = "Validate whether this IP is intentionally shared. If not, remove stale or incorrect records."
            }
        }
    }

    if ($DuplicateResults.Count -eq 0) {
        @() | Write-Output
    } else {
        $DuplicateResults | Write-Output
    }
}
catch {
    throw $_
}
