$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = @()
    try {
        $Forest = Get-ADForest
        if ($ForestFilter -ne 'All' -and -not [string]::IsNullOrEmpty($ForestFilter) -and $Forest.Name -ne $ForestFilter) {
            $Domains = @()
        } else {
            $Domains = if ($DomainFilter -ne 'All' -and -not [string]::IsNullOrEmpty($DomainFilter)) {
                @($DomainFilter)
            } else {
                $Forest.Domains
            }
        }
        foreach ($DomainName in $Domains) {
            $DomainDCs = Get-ADDomainController -Server $DomainName -Credential $cred -Filter *
            if ($SiteFilter -ne 'All' -and -not [string]::IsNullOrEmpty($SiteFilter)) {
                $DomainDCs = $DomainDCs | Where-Object Site -eq $SiteFilter
            }
            if ($DomainDCs) {
                $DCs += $DomainDCs | Select-Object -ExpandProperty HostName
            }
        }
    } catch {
        $DomainDCs = Get-ADDomainController -Credential $cred -Filter *
        if ($SiteFilter -ne 'All' -and -not [string]::IsNullOrEmpty($SiteFilter)) {
            $DomainDCs = $DomainDCs | Where-Object Site -eq $SiteFilter
        }
        $DCs = $DomainDCs | Select-Object -ExpandProperty HostName
    }
} else {
    $DCs = @($TargetDC)
}

if (-not $AuthLookBackHours) { $AuthLookBackHours = 24 }

$results = @()

foreach ($DCName in $DCs) {
    $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName, $AuthLookBackHours -ScriptBlock {
        param($TargetDomain, $TargetDC, $AuthLookBackHours)
        $Computer = $env:COMPUTERNAME
        $AuthStartTime = (Get-Date).AddHours(-$AuthLookBackHours)
        
        $Timings = [System.Collections.Generic.List[object]]::new()
        $totalSw = [System.Diagnostics.Stopwatch]::StartNew()
        $sw = [System.Diagnostics.Stopwatch]::new()

        function Test-IsComputerAccount {
            param([string]$Name)
            if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
            return $Name.EndsWith('$')
        }

        # Event Collection
        $sw.Restart()
        $AllEvents = @()
        try {
            $AllEvents = @(Get-WinEvent -FilterHashtable @{ LogName="Security"; Id=@(4624, 4768, 4769, 4776); StartTime=$AuthStartTime } -ErrorAction SilentlyContinue)
        } catch { }
        
        $Events4624 = [System.Collections.Generic.List[object]]::new()
        $Events4768 = [System.Collections.Generic.List[object]]::new()
        $Events4769 = [System.Collections.Generic.List[object]]::new()
        $Events4776 = [System.Collections.Generic.List[object]]::new()

        # Helper to safely pull property values without XML parsing
        function Get-EvtProp {
            param($Event, [int]$Index)
            try { return $Event.Properties[$Index].Value } catch { return $null }
        }

        foreach ($evt in $AllEvents) {
            switch ($evt.Id) {
                4624 { $Events4624.Add($evt) }
                4768 { $Events4768.Add($evt) }
                4769 { $Events4769.Add($evt) }
                4776 { $Events4776.Add($evt) }
            }
        }
        $sw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'Event Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

        # Successful Logons Processing
        $sw.Restart()
        $SuccessfulLogons = [System.Collections.Generic.List[object]]::new()
        foreach ($evt in $Events4624) {
            $TargetUserName = Get-EvtProp $evt 5
            if ($TargetUserName -and -not (Test-IsComputerAccount $TargetUserName) -and $TargetUserName -notin @("ANONYMOUS LOGON", "SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE")) {
                $Domain = Get-EvtProp $evt 6
                $SuccessfulLogons.Add([PSCustomObject]@{
                    UserName              = "$Domain\$TargetUserName"
                    AuthenticationPackage = Get-EvtProp $evt 10
                    SourceWorkstation     = Get-EvtProp $evt 11
                    TimeCreated           = $evt.TimeCreated
                })
            }
        }
        $sw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'Successful Logons Processing'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

        # Kerberos TGT Processing
        $sw.Restart()
        $KerberosTGTRequests = [System.Collections.Generic.List[object]]::new()
        foreach ($evt in $Events4768) {
            $KerberosTGTRequests.Add([PSCustomObject]@{
                UserName      = Get-EvtProp $evt 0
            })
        }
        $sw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'Kerberos TGT Processing'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

        # Kerberos Service Ticket Processing
        $sw.Restart()
        $sw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'Kerberos Service Ticket Processing'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

        # NTLM Validation Processing
        $sw.Restart()
        $NTLMValidations = [System.Collections.Generic.List[object]]::new()
        foreach ($evt in $Events4776) {
            $NTLMValidations.Add([PSCustomObject]@{
                UserName          = Get-EvtProp $evt 1
            })
        }
        $sw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'NTLM Validation Processing'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

        # Summary Generation
        $sw.Restart()
        $Kerberos4624Count = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -eq "Kerberos" }).Count
        $NTLM4624Count     = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -match "NTLM" }).Count
        $Other4624Count    = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -and $_.AuthenticationPackage -notmatch "Kerberos|NTLM" }).Count
        $KerberosTGTCount  = $Events4768.Count
        $KerberosServiceCount = $Events4769.Count
        $NTLMValidationCount = $Events4776.Count

        $AuthProtocolSummary = @(
            [PSCustomObject]@{ Protocol = "Kerberos - 4624 Logon Package"; Count = $Kerberos4624Count; Notes = "Successful logons using Kerberos package" }
            [PSCustomObject]@{ Protocol = "NTLM - 4624 Logon Package"; Count = $NTLM4624Count; Notes = "Successful logons using NTLM package" }
            [PSCustomObject]@{ Protocol = "Other - 4624 Logon Package"; Count = $Other4624Count; Notes = "Other successful logon packages" }
            [PSCustomObject]@{ Protocol = "Kerberos - TGT Requests 4768"; Count = $KerberosTGTCount; Notes = "Kerberos ticket-granting-ticket requests" }
            [PSCustomObject]@{ Protocol = "Kerberos - Service Tickets 4769"; Count = $KerberosServiceCount; Notes = "Kerberos service ticket requests" }
            [PSCustomObject]@{ Protocol = "NTLM - Credential Validation 4776"; Count = $NTLMValidationCount; Notes = "NTLM validations handled by this DC" }
        )

        $TopAuthenticatedUsers = @($SuccessfulLogons | Where-Object { $_.UserName -and $_.UserName -notmatch "Unable to check" } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count } })
        
        $TopSourceComputers = @($SuccessfulLogons | Where-Object { $_.SourceWorkstation -and $_.SourceWorkstation -notin @("-", "Unknown", "") } | Group-Object SourceWorkstation | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ SourceWorkstation = $_.Name; Count = $_.Count } })
        
        $RecentNTLMUsers = @($NTLMValidations | Where-Object { $_.UserName -and -not (Test-IsComputerAccount $_.UserName) } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count; Status = "WARNING - NTLM observed" } })

        $RecentKerberosUsers = @($KerberosTGTRequests | Where-Object { $_.UserName -and -not (Test-IsComputerAccount $_.UserName) } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count; Status = "OK - Kerberos observed" } })

        $LogonsOverTime = @()

        for ($i = $AuthLookBackHours - 1; $i -ge 0; $i--) {

            $Start = (Get-Date).Date.AddHours((Get-Date).Hour - $i)
            $End   = $Start.AddHours(1)

            $KerberosCount = @(
                $SuccessfulLogons |
                Where-Object {
                    $_.TimeCreated -ge $Start -and
                    $_.TimeCreated -lt $End -and
                    $_.AuthenticationPackage -eq "Kerberos"
                }
            ).Count

            $NTLMCount = @(
                $SuccessfulLogons |
                Where-Object {
                    $_.TimeCreated -ge $Start -and
                    $_.TimeCreated -lt $End -and
                    $_.AuthenticationPackage -match "NTLM"
                }
            ).Count

            $LogonsOverTime += [PSCustomObject]@{
                Time      = $Start.ToString("h tt")
                Kerberos  = $KerberosCount
                NTLM      = $NTLMCount
            }
        }

        $sw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'Summary Generation'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })
        
        $totalSw.Stop()
        $Timings.Add([pscustomobject]@{ Section = 'Total Execution'; ElapsedMilliseconds = $totalSw.ElapsedMilliseconds })

        [PSCustomObject]@{
            ServerName            = $Computer
            FQDN                  = $TargetDC
            AuthProtocolSummary   = $AuthProtocolSummary
            TopAuthenticatedUsers = $TopAuthenticatedUsers
            TopSourceComputers    = $TopSourceComputers
            RecentKerberosUsers   = $RecentKerberosUsers
            RecentNTLMUsers       = $RecentNTLMUsers
            LogonsOverTime        = $LogonsOverTime
            SuccessfulLogons      = $SuccessfulLogons.Count
            KerberosTGTRequests   = $Events4768.Count
            KerberosServiceTickets= $Events4769.Count
            NTLMValidations       = $Events4776.Count
            PerformanceMetrics    = $Timings
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
