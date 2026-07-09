Import-Module DnsServer -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$Findings = New-Object System.Collections.Generic.List[object]

$InvokeErrors = $null
$remoteResults = Invoke-Command -ComputerName $DnsServer -Credential $Credential -ArgumentList $ZoneName -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock {
    param($ZoneName)
    
    $Server = $env:COMPUTERNAME
    $LocalFindings = New-Object System.Collections.Generic.List[object]

    function Add-Finding {
        param($CheckName, $WhatIsChecked, $WhyItMatters, $Result, $Severity, $Recommendation, $Target, $TargetZoneName)
        $null = $LocalFindings.Add([PSCustomObject]@{
            Target         = $Target
            DnsServer      = $Server
            ZoneName       = $TargetZoneName
            CheckName      = $CheckName
            WhatIsChecked  = $WhatIsChecked
            WhyItMatters   = $WhyItMatters
            Result         = $Result
            Severity       = $Severity
            Recommendation = $Recommendation
        })
    }

    $RunServerChecks = $false
    $Zones = @()
    if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
        $RunServerChecks = $true
        try {
            $Zones = @(Get-DnsServerZone -ErrorAction Stop)
        } catch {
            Add-Finding "DNS Zones" "Reads DNS zones on the server." "Zones are required for DNS resolution." "Unable to read DNS zones: $($_.Exception.Message)" "Warning" "Check DNS Server service and permissions." "Server" $null
        }
    } else {
        try {
            $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction Stop)
        } catch {
            Add-Finding "DNS Zones" "Reads specific DNS zone on the server." "Zone is required for DNS resolution." "Unable to read DNS zone $($ZoneName): $($_.Exception.Message)" "Warning" "Check DNS Server service and permissions." "Zone" $ZoneName
        }
    }

    if ($RunServerChecks) {
        # Server level checks
        try {
            $Forwarders = Get-DnsServerForwarder -ErrorAction Stop
            if (!$Forwarders.IPAddress -or $Forwarders.IPAddress.Count -eq 0) {
                Add-Finding "DNS Forwarders" "Checks whether the DNS server has forwarders configured." "Forwarders allow consistent external name resolution." "No DNS forwarders configured." "Warning" "Configure approved forwarders." "Server" $null
            } else {
                Add-Finding "DNS Forwarders" "Checks whether the DNS server has forwarders configured." "Forwarders allow consistent external name resolution." "Forwarders configured: $($Forwarders.IPAddress -join ', ')" "Passed" "No action required." "Server" $null
            }
        } catch {
            Add-Finding "DNS Forwarders" "Reads DNS forwarder configuration." "Forwarders are important for external DNS resolution." "Unable to read forwarders." "Warning" "Check DNS Server service and permissions." "Server" $null
        }

        try {
            $Scavenging = Get-DnsServerScavenging -ErrorAction Stop
            if ($Scavenging.ScavengingState -ne $true) {
                Add-Finding "DNS Scavenging" "Checks whether scavenging is enabled." "Without scavenging, stale dynamic records may remain." "Scavenging is disabled." "Warning" "Enable scavenging carefully." "Server" $null
            } else {
                Add-Finding "DNS Scavenging" "Checks whether scavenging is enabled." "Scavenging removes old dynamic records." "Enabled. NoRefresh: $($Scavenging.NoRefreshInterval)" "Passed" "No action required." "Server" $null
            }
        } catch {
            Add-Finding "DNS Scavenging" "Reads DNS scavenging configuration." "Scavenging helps clean old DNS records." "Unable to read scavenging settings." "Warning" "Check DNS Server service." "Server" $null
        }

        try {
            $Recursion = Get-DnsServerRecursion -ErrorAction Stop
            if ($Recursion.Enable -eq $true) {
                Add-Finding "DNS Recursion" "Checks whether recursive DNS resolution is enabled." "Internal AD DNS servers normally need recursion." "Recursion is enabled." "Passed" "No action required." "Server" $null
            } else {
                Add-Finding "DNS Recursion" "Checks whether recursive DNS resolution is enabled." "AD clients may require recursive resolution." "Recursion is disabled." "Warning" "Enable recursion for normal AD DNS." "Server" $null
            }
        } catch {
            Add-Finding "DNS Recursion" "Reads DNS recursion configuration." "Internal AD clients may depend on recursive DNS resolution." "Unable to read recursion settings." "Warning" "Check DNS Server service." "Server" $null
        }
    }

    # Zone level checks
    foreach ($Zone in $Zones) {
        $CurrentZoneName = $Zone.ZoneName

        if ($Zone.ZoneType -eq "Primary" -and $Zone.IsDsIntegrated -ne $true) {
            Add-Finding "AD-Integrated DNS Zone" "Checks whether a primary DNS zone is AD-integrated." "AD-integrated zones replicate securely." "Zone is primary but not AD-integrated." "Warning" "Use AD-integrated DNS." "Zone" $CurrentZoneName
        } else {
            Add-Finding "AD-Integrated DNS Zone" "Checks whether a primary DNS zone is AD-integrated." "AD-integrated zones replicate securely." "Zone type: $($Zone.ZoneType), AD-integrated: $($Zone.IsDsIntegrated)." "Passed" "No action required." "Zone" $CurrentZoneName
        }

        if ($Zone.ZoneType -eq "Primary" -and $Zone.IsDsIntegrated -eq $true -and $Zone.IsReverseLookupZone -ne $true) {
            if ($Zone.DynamicUpdate -ne "Secure") {
                Add-Finding "Secure Dynamic Updates" "Checks whether AD-integrated zone allows only secure dynamic updates." "Non-secure updates may allow unauthorized devices to register." "Dynamic update is: $($Zone.DynamicUpdate)." "Warning" "Use Secure Only dynamic updates." "Zone" $CurrentZoneName
            } else {
                Add-Finding "Secure Dynamic Updates" "Checks whether AD-integrated zone allows only secure dynamic updates." "Secure updates protect DNS registration." "Secure dynamic updates are enabled." "Passed" "No action required." "Zone" $CurrentZoneName
            }
        }

        if ($Zone.IsDsIntegrated -eq $true) {
            if ([string]::IsNullOrWhiteSpace($Zone.ReplicationScope)) {
                Add-Finding "Zone Replication Scope" "Checks the AD replication scope for this DNS zone." "Correct scope ensures data reaches right servers." "Replication scope could not be confirmed." "Warning" "Validate zone replication scope." "Zone" $CurrentZoneName
            } else {
                Add-Finding "Zone Replication Scope" "Checks the AD replication scope for this DNS zone." "Correct scope ensures data reaches right servers." "Replication scope is: $($Zone.ReplicationScope)." "Passed" "No action required." "Zone" $CurrentZoneName
            }
        }

        if ($Zone.IsPaused -eq $true -or $Zone.IsShutdown -eq $true) {
            Add-Finding "Zone Runtime State" "Checks whether the DNS zone is paused or shutdown." "A paused zone may stop serving DNS records." "Zone paused: $($Zone.IsPaused), shutdown: $($Zone.IsShutdown)." "Critical" "Investigate DNS service state immediately." "Zone" $CurrentZoneName
        } else {
            Add-Finding "Zone Runtime State" "Checks whether the DNS zone is paused or shutdown." "Healthy zones should be online." "Zone is not paused or shutdown." "Passed" "No action required." "Zone" $CurrentZoneName
        }
    }

    return $LocalFindings
}

if ($remoteResults) {
    foreach ($res in $remoteResults) {
        $null = $Findings.Add([PSCustomObject]@{
            Target         = $res.Target
            DnsServer      = $res.DnsServer
            ZoneName       = $res.ZoneName
            CheckName      = $res.CheckName
            WhatIsChecked  = $res.WhatIsChecked
            WhyItMatters   = $res.WhyItMatters
            Result         = $res.Result
            Severity       = $res.Severity
            Recommendation = $res.Recommendation
        })
    }
}

if (-not [string]::IsNullOrWhiteSpace($HealthFilter) -and $HealthFilter -ne 'All') {
    if ($HealthFilter -eq 'Healthy') {
        $FilteredFindings = $Findings | Where-Object { $_.Severity -eq 'Passed' }
    } else {
        $FilteredFindings = $Findings | Where-Object { $_.Severity -eq $HealthFilter }
    }
} else {
    $FilteredFindings = $Findings
}

if ($FilteredFindings.Count -eq 0) {
    @() | Write-Output
} else {
    $FilteredFindings | Write-Output
}
