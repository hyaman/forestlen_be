

$ResolutionTests = @()

try {
    $Forest = Get-ADForest -ErrorAction Stop
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
    
    if ($ResolutionTests.Count -eq 0) {
        @() | Write-Output
    } else {
        $ResolutionTests | Write-Output
    }
}
catch {
    throw $_
}
