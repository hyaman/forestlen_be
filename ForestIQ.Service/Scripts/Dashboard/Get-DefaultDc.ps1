# TargetDomain is injected by DashboardService
$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

Import-Module ActiveDirectory -ErrorAction SilentlyContinue

try {
    # If no target domain is provided, try to determine the current domain
    if ([string]::IsNullOrEmpty($TargetDomain) -or $TargetDomain -eq "All") {
        $TargetDomain = (Get-ADDomain -Credential $cred).Name
    }

    $forest = Get-ADForest -Credential $cred -ErrorAction Stop
    $domainNamingMaster = $forest.DomainNamingMaster

    $targetDcName = ""

    # Check all domains in the forest using standard loop (fast AD query)
    foreach ($domainName in $forest.Domains) {
        try {
            $domain = Get-ADDomain -Identity $domainName -Credential $cred -ErrorAction Stop
            if ($domain.InfrastructureMaster -eq $domainNamingMaster) {
                $targetDcName = $domainNamingMaster
                break
            }
        }
        catch { }
    }

    if ([string]::IsNullOrWhiteSpace($targetDcName)) {
        $targetDcName = $env:COMPUTERNAME
    }

    # Get details of the selected DC
    $dcInfo = Get-ADDomainController -Identity $targetDcName -Credential $cred -ErrorAction Stop

    $result = [PSCustomObject]@{
        ServerName      = $dcInfo.Name
        FQDN            = $dcInfo.HostName
        IPv4            = $dcInfo.IPv4Address
        SiteName        = $dcInfo.Site
        DomainName      = $dcInfo.Domain
        ForestName      = $dcInfo.Forest
        IsGlobalCatalog = $dcInfo.IsGlobalCatalog
        IsReadOnly      = $dcInfo.IsReadOnly
        OperatingSystem = $dcInfo.OperatingSystem
    }

    $result

}
catch {
    Write-Error $_
    exit 1
}
