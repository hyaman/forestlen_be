Import-Module ActiveDirectory -ErrorAction Stop

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

try {
    $forest = Get-ADForest -Credential $cred
    [pscustomobject]@{ Items = @($forest.Domains) }
} catch {
    throw $_
}
