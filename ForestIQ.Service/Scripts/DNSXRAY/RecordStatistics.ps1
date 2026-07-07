Import-Module DnsServer -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$Stats = @()

$InvokeErrors = $null
$remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ArgumentList $ZoneName -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock {
    param($ZoneName)
    $Server = $env:COMPUTERNAME
    $LocalStats = @()
    try {
        if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
            $Zones = Get-DnsServerZone -ErrorAction Stop
        } else {
            $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction Stop)
        }

        foreach ($Zone in $Zones) {
            $CurrentZoneName = $Zone.ZoneName
            $Records = Get-DnsServerResourceRecord -ZoneName $CurrentZoneName -ErrorAction SilentlyContinue

            $Grouped = $Records | Group-Object RecordType
            foreach ($Group in $Grouped) {
                $LocalStats += [PSCustomObject]@{
                    DnsServer  = $Server
                    ZoneName   = $CurrentZoneName
                    RecordType = $Group.Name
                    Count      = $Group.Count
                }
            }
        }
    } catch {
        # Silently skip
    }
    return $LocalStats
}

if ($remoteResults) {
    foreach ($res in $remoteResults) {
        $Stats += [PSCustomObject]@{
            DnsServer  = $res.DnsServer
            ZoneName   = $res.ZoneName
            RecordType = $res.RecordType
            Count      = $res.Count
        }
    }
}

if ($Stats.Count -eq 0) {
    @() | Write-Output
} else {
    $Stats | Write-Output
}
