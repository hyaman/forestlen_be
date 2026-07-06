

$DnsServers = @()

try {
    $Forest = Get-ADForest -ErrorAction Stop
    
    $Domains = $Forest.Domains
    if ($DomainFilter -ne 'All') {
        $Domains = $Domains | Where-Object { $_ -like "*$DomainFilter*" }
    }
    
    foreach ($Domain in $Domains) {
        $DCs = Get-ADDomainController -Filter * -Server $Domain -ErrorAction Stop
        
        if ($SiteFilter -ne 'All') {
            $DCs = $DCs | Where-Object { $_.Site -eq $SiteFilter }
        }
        if ($TargetDC -ne 'All') {
            $DCs = $DCs | Where-Object { $_.HostName -eq $TargetDC -or $_.Name -eq $TargetDC }
        }

        foreach ($DC in $DCs) {
            $existing = $DnsServers | Where-Object { $_.DnsServer -eq $DC.HostName }
            if (-not $existing) {
                $DnsServers += [PSCustomObject]@{ DnsServer = $DC.HostName }
            }
        }
    }
    
    if ($DnsServers.Count -eq 0) {
        @() | Write-Output
    } else {
        $DnsServers | Write-Output
    }
}
catch {
    throw $_
}
