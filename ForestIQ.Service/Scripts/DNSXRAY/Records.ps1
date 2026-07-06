

Import-Module DnsServer -ErrorAction Stop

$DnsInventory = @()

try {
    $Records = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -ErrorAction Stop

    $Zone = Get-DnsServerZone -ComputerName $DnsServer -ZoneName $ZoneName -ErrorAction Stop

    foreach ($Record in $Records) {
        $RecordData = Get-RecordDataValue -Record $Record
        $AgeStatus = Get-RecordAgeStatus -Timestamp $Record.Timestamp -StaleDays $StaleRecordDays
        $AgeDays = Get-RecordAgeDays -Timestamp $Record.Timestamp

        if ($Record.HostName -eq "@") {
            $FQDN = $ZoneName
        } else {
            $FQDN = "$($Record.HostName).$($ZoneName)"
        }

        $DnsInventory += [PSCustomObject]@{
            DnsServer        = $DnsServer
            ZoneName         = $ZoneName
            ZoneType         = $Zone.ZoneType
            IsDsIntegrated   = $Zone.IsDsIntegrated
            ReplicationScope = $Zone.ReplicationScope
            RecordName       = $Record.HostName
            FQDN             = $FQDN
            RecordType       = $Record.RecordType
            RecordData       = $RecordData
            Timestamp        = $Record.Timestamp
            AgeDays          = $AgeDays
            AgeStatus        = $AgeStatus
            TTL              = $Record.TimeToLive.TotalSeconds
        }
    }
    
    if ($DnsInventory.Count -eq 0) {
        @() | Write-Output
    } else {
        $DnsInventory | Write-Output
    }
}
catch {
    throw $_
}
