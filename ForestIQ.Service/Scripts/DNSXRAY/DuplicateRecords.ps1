Import-Module DnsServer -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$HelpersContent = ""
foreach ($func in @('Get-RecordDataValue', 'Get-RecordAgeDays', 'Get-RecordAgeStatus', 'Test-DnsNameExistsInInventory')) {
    if (Get-Command $func -ErrorAction SilentlyContinue) {
        $HelpersContent += "function $func { $((Get-Command $func).Definition) }`n"
    }
}

$ScriptBlockStr = 'param($ZoneName)' + "`n" + $HelpersContent + "`n" + @'

$Server = $env:COMPUTERNAME
$DuplicateResults = @()
try {
    if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
        $Zones = Get-DnsServerZone -ErrorAction Stop
    } else {
        $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction Stop)
    }

    $DnsInventory = @()
    foreach ($Zone in $Zones) {
        $CurrentZoneName = $Zone.ZoneName
        $Records = Get-DnsServerResourceRecord -ZoneName $CurrentZoneName -ErrorAction SilentlyContinue

        foreach ($Record in $Records) {
            $RecordData = Get-RecordDataValue -Record $Record
            if ($Record.HostName -eq "@") { $FQDN = $CurrentZoneName } else { $FQDN = "$($Record.HostName).$($CurrentZoneName)" }

            $DnsInventory += [PSCustomObject]@{
                DnsServer  = $Server
                ZoneName   = $CurrentZoneName
                FQDN       = $FQDN
                RecordType = $Record.RecordType
                RecordData = $RecordData
            }
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
} catch {
    # Silently skip errors
}
return $DuplicateResults
'@

$ScriptBlock = [scriptblock]::Create($ScriptBlockStr)

$InvokeErrors = $null
$remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ArgumentList $ZoneName -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock $ScriptBlock

$FinalResults = @()
if ($remoteResults) {
    foreach ($res in $remoteResults) {
        $FinalResults += [PSCustomObject]@{
            DuplicateType  = $res.DuplicateType
            DnsServer      = $res.DnsServer
            ZoneName       = $res.ZoneName
            FQDN           = $res.FQDN
            RecordType     = $res.RecordType
            RecordData     = $res.RecordData
            Count          = $res.Count
            Recommendation = $res.Recommendation
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($HealthFilter) -and $HealthFilter -ne 'All') {
    if ($HealthFilter -ne 'Warning') {
        $FinalResults = @()
    }
}

if ($FinalResults.Count -eq 0) {
    @() | Write-Output
} else {
    $FinalResults | Write-Output
}
