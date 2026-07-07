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
$IntegrityFindings = @()
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
                RecordName = $Record.HostName
                FQDN       = $FQDN
                RecordType = $Record.RecordType
                RecordData = $RecordData
            }
        }
    }

    $ARecords     = $DnsInventory | Where-Object { $_.RecordType -eq "A" }
    $PTRRecords   = $DnsInventory | Where-Object { $_.RecordType -eq "PTR" }
    $CNAMERecords = $DnsInventory | Where-Object { $_.RecordType -eq "CNAME" }
    $MXRecords    = $DnsInventory | Where-Object { $_.RecordType -eq "MX" }
    $SRVRecords   = $DnsInventory | Where-Object { $_.RecordType -eq "SRV" }

    foreach ($A in $ARecords) {
        $MatchingPTR = $PTRRecords | Where-Object {
            $_.RecordData.TrimEnd(".").ToLower() -eq $A.FQDN.TrimEnd(".").ToLower()
        } | Select-Object -First 1

        if (!$MatchingPTR) {
            $IntegrityFindings += [PSCustomObject]@{
                FindingType    = "A Record Missing PTR"
                Severity       = "Warning"
                DnsServer      = $A.DnsServer
                ZoneName       = $A.ZoneName
                RecordName     = $A.FQDN
                RecordType     = $A.RecordType
                RecordData     = $A.RecordData
                Recommendation = "Create PTR if reverse lookup is required."
            }
        }
    }

    foreach ($PTR in $PTRRecords) {
        $MatchingA = $ARecords | Where-Object {
            $_.FQDN.TrimEnd(".").ToLower() -eq $PTR.RecordData.TrimEnd(".").ToLower()
        } | Select-Object -First 1

        if (!$MatchingA) {
            $IntegrityFindings += [PSCustomObject]@{
                FindingType    = "PTR Record Missing A"
                Severity       = "Warning"
                DnsServer      = $PTR.DnsServer
                ZoneName       = $PTR.ZoneName
                RecordName     = $PTR.FQDN
                RecordType     = $PTR.RecordType
                RecordData     = $PTR.RecordData
                Recommendation = "Remove orphaned PTR or recreate the missing A record."
            }
        }
    }

    foreach ($CNAME in $CNAMERecords) {
        $Exists = Test-DnsNameExistsInInventory -Target $CNAME.RecordData -Inventory $DnsInventory
        if (!$Exists) {
            $IntegrityFindings += [PSCustomObject]@{
                FindingType    = "Broken CNAME Target"
                Severity       = "Critical"
                DnsServer      = $CNAME.DnsServer
                ZoneName       = $CNAME.ZoneName
                RecordName     = $CNAME.FQDN
                RecordType     = $CNAME.RecordType
                RecordData     = $CNAME.RecordData
                Recommendation = "Update the CNAME target or remove the record."
            }
        }
    }

    foreach ($MX in $MXRecords) {
        $Exists = Test-DnsNameExistsInInventory -Target $MX.RecordData -Inventory $DnsInventory
        if (!$Exists) {
            $IntegrityFindings += [PSCustomObject]@{
                FindingType    = "Broken MX Target"
                Severity       = "Critical"
                DnsServer      = $MX.DnsServer
                ZoneName       = $MX.ZoneName
                RecordName     = $MX.FQDN
                RecordType     = $MX.RecordType
                RecordData     = $MX.RecordData
                Recommendation = "Correct the MX target or remove the record."
            }
        }
    }

    foreach ($SRV in $SRVRecords) {
        $Exists = Test-DnsNameExistsInInventory -Target $SRV.RecordData -Inventory $DnsInventory
        if (!$Exists) {
            $IntegrityFindings += [PSCustomObject]@{
                FindingType    = "Broken SRV Target"
                Severity       = "Critical"
                DnsServer      = $SRV.DnsServer
                ZoneName       = $SRV.ZoneName
                RecordName     = $SRV.FQDN
                RecordType     = $SRV.RecordType
                RecordData     = $SRV.RecordData
                Recommendation = "Validate AD service registration, Netlogon, and DNS replication."
            }
        }
    }
} catch {
    # Silently skip errors
}
return $IntegrityFindings
'@
$ScriptBlock = [scriptblock]::Create($ScriptBlockStr)

$InvokeErrors = $null
$remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ArgumentList $ZoneName -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock $ScriptBlock

$FinalFindings = @()
if ($remoteResults) {
    foreach ($res in $remoteResults) {
        $FinalFindings += [PSCustomObject]@{
            FindingType    = $res.FindingType
            Severity       = $res.Severity
            DnsServer      = $res.DnsServer
            ZoneName       = $res.ZoneName
            RecordName     = $res.RecordName
            RecordType     = $res.RecordType
            RecordData     = $res.RecordData
            Recommendation = $res.Recommendation
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($HealthFilter) -and $HealthFilter -ne 'All') {
    $FinalFindings = $FinalFindings | Where-Object { $_.Severity -eq $HealthFilter }
}

if ($FinalFindings.Count -eq 0) {
    @() | Write-Output
} else {
    $FinalFindings | Write-Output
}
