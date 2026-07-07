Import-Module DnsServer -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$DnsInventory = @()

$HelpersContent = ""
foreach ($func in @('Get-RecordDataValue', 'Get-RecordAgeDays', 'Get-RecordAgeStatus', 'Test-DnsNameExistsInInventory')) {
    if (Get-Command $func -ErrorAction SilentlyContinue) {
        $HelpersContent += "function $func { $((Get-Command $func).Definition) }`n"
    }
}

$ScriptBlockStr = 'param($ZoneName, $StaleRecordDays)' + "`n" + $HelpersContent + "`n" + @'

$Server = $env:COMPUTERNAME
$LocalInventory = @()
try {
    if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
        $Zones = @(Get-DnsServerZone -ErrorAction SilentlyContinue)
    } else {
        $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction SilentlyContinue)
    }

    foreach ($Zone in $Zones) {
        $CurrentZoneName = $Zone.ZoneName
        $Records = Get-DnsServerResourceRecord -ZoneName $CurrentZoneName -ErrorAction SilentlyContinue

        foreach ($Record in $Records) {
            $RecordData = Get-RecordDataValue -Record $Record
            $AgeStatus = Get-RecordAgeStatus -Timestamp $Record.Timestamp -StaleDays $StaleRecordDays
            $AgeDays = Get-RecordAgeDays -Timestamp $Record.Timestamp

            if ($AgeDays -ne $null -and $AgeDays -gt $StaleRecordDays) {
                if ($Record.HostName -eq "@") {
                    $FQDN = $CurrentZoneName
                } else {
                    $FQDN = "$($Record.HostName).$($CurrentZoneName)"
                }

                $LocalInventory += [PSCustomObject]@{
                    DnsServer        = $Server
                    ZoneName         = $CurrentZoneName
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
        }
    }
} catch {
    # Silently skip
}
return $LocalInventory
'@

$ScriptBlock = [scriptblock]::Create($ScriptBlockStr)

$InvokeErrors = $null
$remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ArgumentList $ZoneName, $StaleRecordDays -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock $ScriptBlock

if ($remoteResults) {
    foreach ($res in $remoteResults) {
        $DnsInventory += [PSCustomObject]@{
            DnsServer        = $res.DnsServer
            ZoneName         = $res.ZoneName
            ZoneType         = $res.ZoneType
            IsDsIntegrated   = $res.IsDsIntegrated
            ReplicationScope = $res.ReplicationScope
            RecordName       = $res.RecordName
            FQDN             = $res.FQDN
            RecordType       = $res.RecordType
            RecordData       = $res.RecordData
            Timestamp        = $res.Timestamp
            AgeDays          = $res.AgeDays
            AgeStatus        = $res.AgeStatus
            TTL              = $res.TTL
        }
    }
}

if ($CountOnly -eq $true) {
    [PSCustomObject]@{ Count = $DnsInventory.Count } | Write-Output
} else {
    if ($DnsInventory.Count -eq 0) {
        @() | Write-Output
    } else {
        $DnsInventory | Write-Output
    }
}
