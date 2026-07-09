function Get-RecordDataValue {
    param($Record)

    try {
        switch ($Record.RecordType) {
            "A"     { return $Record.RecordData.IPv4Address.IPAddressToString }
            "AAAA"  { return $Record.RecordData.IPv6Address.IPAddressToString }
            "CNAME" { return $Record.RecordData.HostNameAlias }
            "MX"    { return $Record.RecordData.MailExchange }
            "NS"    { return $Record.RecordData.NameServer }
            "SRV"   { return $Record.RecordData.DomainName }
            "PTR"   { return $Record.RecordData.PtrDomainName }
            "TXT"   { return ($Record.RecordData.DescriptiveText -join " ") }
            "SOA"   { return $Record.RecordData.PrimaryServer }
            default { return ($Record.RecordData | Out-String).Trim() }
        }
    }
    catch {
        return "Unable to parse record data"
    }
}

function Get-RecordAgeStatus {
    param(
        $Timestamp,
        [int]$StaleDays
    )

    if ($null -eq $Timestamp -or $Timestamp -eq [datetime]"1601-01-01" -or $Timestamp -eq 0) {
        return "Static"
    }

    $AgeDays = ((Get-Date) - $Timestamp).Days

    if ($AgeDays -ge $StaleDays) {
        return "Stale"
    }

    return "Active"
}

function Get-RecordAgeDays {
    param($Timestamp)

    if ($null -eq $Timestamp -or $Timestamp -eq [datetime]"1601-01-01" -or $Timestamp -eq 0) {
        return $null
    }

    return ((Get-Date) - $Timestamp).Days
}

function Test-DnsNameExistsInInventory {
    param(
        [string]$Target,
        [array]$Inventory
    )

    if ([string]::IsNullOrWhiteSpace($Target)) {
        return $false
    }

    $CleanTarget = $Target.TrimEnd(".").ToLower()

    $Match = $Inventory | Where-Object {
        $_.FQDN.TrimEnd(".").ToLower() -eq $CleanTarget -or
        $_.RecordName.TrimEnd(".").ToLower() -eq $CleanTarget
    } | Select-Object -First 1

    return [bool]$Match
}
