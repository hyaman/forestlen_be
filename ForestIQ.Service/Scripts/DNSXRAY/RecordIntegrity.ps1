

Import-Module DnsServer -ErrorAction Stop

$IntegrityFindings = @()

try {
    $Records = Get-DnsServerResourceRecord -ComputerName $DnsServer -ZoneName $ZoneName -ErrorAction Stop

    $DnsInventory = @()
    foreach ($Record in $Records) {
        $RecordData = Get-RecordDataValue -Record $Record
        if ($Record.HostName -eq "@") { $FQDN = $ZoneName } else { $FQDN = "$($Record.HostName).$($ZoneName)" }

        $DnsInventory += [PSCustomObject]@{
            DnsServer  = $DnsServer
            ZoneName   = $ZoneName
            RecordName = $Record.HostName
            FQDN       = $FQDN
            RecordType = $Record.RecordType
            RecordData = $RecordData
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

    if ($IntegrityFindings.Count -eq 0) {
        @() | Write-Output
    } else {
        $IntegrityFindings | Write-Output
    }
}
catch {
    throw $_
}
