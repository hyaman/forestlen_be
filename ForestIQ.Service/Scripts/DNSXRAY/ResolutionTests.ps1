

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

$ResolutionTests = @()

try {
    $ADParams = @{}
    if ($Credential) {
        $ADParams.Credential = $Credential
        if ($global:RemoteDomain) {
            $ADParams.Server = $global:RemoteDomain
        }
    }
    $Forest = Get-ADForest @ADParams -ErrorAction Stop
    $TestNames = @(
        $Forest.Name,
        "_ldap._tcp.dc._msdcs.$($Forest.Name)",
        "_kerberos._tcp.$($Forest.Name)"
    )

    foreach ($TestName in $TestNames) {
        try {
            $Result = Resolve-DnsName -Name $TestName -Server $DnsServer -ErrorAction Stop
            
            $Summary = ($Result | Select-Object -First 3 | ForEach-Object {
                if ($_.IPAddress) { "$($_.Type):$($_.IPAddress)" }
                elseif ($_.NameHost) { "$($_.Type):$($_.NameHost)" }
                else { "$($_.Type):$($_.Name)" }
            }) -join "; "
            
            $ResolutionTests += [PSCustomObject]@{
                DnsServer      = $DnsServer
                QueryName      = $TestName
                Status         = "Passed"
                AnswerCount    = $Result.Count
                ResultSummary  = $Summary
                Recommendation = "No action required."
            }
        } catch {
            $ResolutionTests += [PSCustomObject]@{
                DnsServer      = $DnsServer
                QueryName      = $TestName
                Status         = "Failed"
                AnswerCount    = 0
                ResultSummary  = $_.Exception.Message
                Recommendation = "Check AD SRV records, Netlogon, DNS zone health, and replication."
            }
        }
    }
    
    if (-not [string]::IsNullOrWhiteSpace($HealthFilter) -and $HealthFilter -ne 'All') {
        if ($HealthFilter -eq 'Healthy') {
            $ResolutionTests = $ResolutionTests | Where-Object { $_.Status -eq 'Passed' }
        } elseif ($HealthFilter -eq 'Critical') {
            $ResolutionTests = $ResolutionTests | Where-Object { $_.Status -eq 'Failed' }
        } else {
            $ResolutionTests = @()
        }
    }

    if ($ResolutionTests.Count -eq 0) {
        @() | Write-Output
    } else {
        $ResolutionTests | Write-Output
    }
}
catch {
    throw $_
}
