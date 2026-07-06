

Import-Module DnsServer -ErrorAction Stop

$Stats = @()

try {
    $Records = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -ErrorAction Stop

    $Grouped = $Records | Group-Object RecordType
    foreach ($Group in $Grouped) {
        $Stats += [PSCustomObject]@{
            DnsServer  = $DnsServer
            ZoneName   = $ZoneName
            RecordType = $Group.Name
            Count      = $Group.Count
        }
    }
    
    if ($Stats.Count -eq 0) {
        @() | Write-Output
    } else {
        $Stats | Write-Output
    }
}
catch {
    throw $_
}
