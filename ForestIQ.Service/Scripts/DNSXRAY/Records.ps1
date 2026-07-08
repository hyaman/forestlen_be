

Import-Module DnsServer -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$DnsInventory = @()

try {
    $HelpersContent = ""
    foreach ($func in @('Get-RecordDataValue', 'Get-RecordAgeDays', 'Get-RecordAgeStatus', 'Test-DnsNameExistsInInventory')) {
        if (Get-Command $func -ErrorAction SilentlyContinue) {
            $HelpersContent += "function $func { $((Get-Command $func).Definition) }`n"
        }
    }

    $ScriptBlockStr = 'param($ZoneName, $StaleRecordDays, $DnsServerName)' + "`n" + $HelpersContent + "`n" + @'
    
    $LocalInventory = [System.Collections.Generic.List[PSCustomObject]]::new()
    if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
        $Zones = Get-DnsServerZone -ErrorAction Stop
    } else {
        $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction Stop)
    }

    foreach ($Zone in $Zones) {
        $CurrentZoneName = $Zone.ZoneName
        $Records = Get-DnsServerResourceRecord -ZoneName $CurrentZoneName -ErrorAction SilentlyContinue

        foreach ($Record in $Records) {
            $RecordData = Get-RecordDataValue -Record $Record
            $AgeStatus = Get-RecordAgeStatus -Timestamp $Record.Timestamp -StaleDays $StaleRecordDays
            $AgeDays = Get-RecordAgeDays -Timestamp $Record.Timestamp

            if ($Record.HostName -eq "@") {
                $FQDN = $CurrentZoneName
            } else {
                $FQDN = "$($Record.HostName).$($CurrentZoneName)"
            }

            $LocalInventory.Add([PSCustomObject]@{
                DnsServer        = $DnsServerName
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
            })
        }
    }
    if ($LocalInventory.Count -gt 0) {
        return ($LocalInventory | ConvertTo-Json -Depth 5 -Compress)
    }
'@
    $ScriptBlock = [scriptblock]::Create($ScriptBlockStr)

    $InvokeErrors = $null
    $remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ArgumentList $ZoneName, $StaleRecordDays, $DnsServer -ScriptBlock $ScriptBlock

    $DnsInventory = if ($remoteResults) {
        foreach ($resStr in $remoteResults) {
            $parsed = $resStr | ConvertFrom-Json
            if ($null -ne $parsed) {
                $parsed
            }
        }
    } else {
        @()
    }
    
    $DnsInventory | Write-Output
}
catch {
    throw $_
}
