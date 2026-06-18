Import-Module ActiveDirectory -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

try {
    $sites = @(Get-ADReplicationSite -Filter * -Credential $cred | Select-Object -ExpandProperty Name)
    [pscustomobject]@{ Items = $sites }
} catch {
    throw $_
}
