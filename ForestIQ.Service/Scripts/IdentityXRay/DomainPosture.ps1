param(
    [string]$Domain
)
$ErrorActionPreference = "Stop"

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$Credential = $null
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {
    $Ctx = Get-DomainContext -DomainName $Domain
    $DCs = @(Get-ADDomainController -Server $Domain -Filter * -Credential $Credential)
    $Trusts = @(Get-ADTrust -Server $Domain -Filter * -Properties * -Credential $Credential)
    $Policy = Get-ADDefaultDomainPasswordPolicy -Server $Domain -Credential $Credential
    
    $HasLegacyLapsPwd = Test-SchemaAttribute $Domain "ms-Mcs-AdmPwd"
    $HasLegacyLapsExp = Test-SchemaAttribute $Domain "ms-Mcs-AdmPwdExpirationTime"
    $HasWinLapsPwd = Test-SchemaAttribute $Domain "msLAPS-Password"
    $HasWinLapsEnc = Test-SchemaAttribute $Domain "msLAPS-EncryptedPassword"
    $HasWinLapsExp = Test-SchemaAttribute $Domain "msLAPS-PasswordExpirationTime"

    $Fgpps = @(Get-ADFineGrainedPasswordPolicy -Server $Domain -Filter * -Properties * -Credential $Credential)

    $FspContainer = "CN=ForeignSecurityPrincipals,$($Ctx.DistinguishedName)"
    $Fsps = @()
    try {
        $Fsps = @(Get-ADObject -Server $Domain -SearchBase $FspContainer -LDAPFilter "(objectClass=foreignSecurityPrincipal)" -Properties Name,ObjectSid -Credential $Credential)
    } catch {}

    $Result = [PSCustomObject]@{
        Domain = $Domain
        DN = $Ctx.DistinguishedName
        DCs = $DCs | Select-Object HostName,Site,IPv4Address,IsGlobalCatalog,OperatingSystem
        Trusts = $Trusts | Select-Object Name,Direction,TrustType,Transitive,SelectiveAuthentication,SIDFilteringQuarantined
        PasswordPolicy = $Policy | Select-Object ComplexityEnabled,MinPasswordLength,MaxPasswordAge,PasswordHistoryCount,LockoutThreshold,LockoutDuration,LockoutObservationWindow
        LapsSchemaStatus = [PSCustomObject]@{
            LegacyLapsPassword = $HasLegacyLapsPwd
            LegacyLapsExpiration = $HasLegacyLapsExp
            WindowsLapsPassword = $HasWinLapsPwd
            WindowsLapsEncrypted = $HasWinLapsEnc
            WindowsLapsExpiration = $HasWinLapsExp
        }
        FGPPs = $Fgpps | Select-Object Name,Precedence,MinPasswordLength,MaxPasswordAge,LockoutThreshold,AppliesTo
        ForeignPrincipals = $Fsps | Select-Object Name,ObjectClass
    }
    
    Write-Output $Result
} catch {
    throw $_
}
