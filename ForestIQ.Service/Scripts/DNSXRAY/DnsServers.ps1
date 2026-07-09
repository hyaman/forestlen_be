
$DnsServers = @()

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {
    $ADParams = @{}
    if ($Credential) {
        $ADParams.Credential = $Credential
        if ($global:RemoteDomain) {
            $ADParams.Server = $global:RemoteDomain
        }
    }

    $Forest = Get-ADForest @ADParams -ErrorAction Stop
    
    $Domains = $Forest.Domains
    if ($DomainFilter -ne 'All') {
        $Domains = $Domains | Where-Object { $_ -like "*$DomainFilter*" }
    }
    
    foreach ($Domain in $Domains) {
        $DomainADParams = $ADParams.Clone()
        $DomainADParams.Server = $Domain
        $DCs = Get-ADDomainController -Filter * @DomainADParams -ErrorAction Stop
        
        if ($SiteFilter -ne 'All') {
            $DCs = $DCs | Where-Object { $_.Site -eq $SiteFilter }
        }
        if ($TargetDC -ne 'All') {
            $DCs = $DCs | Where-Object { $_.HostName -eq $TargetDC -or $_.Name -eq $TargetDC }
        }

        foreach ($DC in $DCs) {
            $existing = $DnsServers | Where-Object { $_.DnsServer -eq $DC.HostName }
            if (-not $existing) {
                $DnsServers += [PSCustomObject]@{ 
                    DnsServer = $DC.HostName 
                    IpAddress = $DC.IPv4Address
                }
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
