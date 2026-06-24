$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

Import-Module ActiveDirectory -ErrorAction SilentlyContinue

$results = @()
$Forest = $null

try {
    $Forest = Get-ADForest
} catch {
    Write-Error "Failed to get AD Forest."
    return $results
}

$Domains = if ($DomainFilter -ne 'All' -and -not [string]::IsNullOrEmpty($DomainFilter)) {
    @($DomainFilter)
} else {
    $Forest.Domains
}

foreach ($Domain in $Domains) {
    try {
        $DCs = Get-ADDomainController -Server $Domain -Credential $cred -Filter *
        if ($SiteFilter -ne 'All' -and -not [string]::IsNullOrEmpty($SiteFilter)) {
            $DCs = $DCs | Where-Object { $_.Site -eq $SiteFilter }
        }

        foreach ($DC in $DCs) {

            $results += [PSCustomObject]@{
                ForestName = $Forest.Name
                DomainName = $Domain
                SiteName   = $DC.Site
                ServerName = $DC.Name
                FQDN       = $DC.HostName
                IPv4       = $DC.IPv4Address
            }
        }
    } catch {
        # Domain not reachable or no DCs
    }
}

return $results
