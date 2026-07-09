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
    $DomainDN = $Ctx.DistinguishedName

    $AclTargets = New-Object System.Collections.Generic.List[object]
    $AclTargets.Add([PSCustomObject]@{ Name="Domain Root"; Type="Domain"; DN=$DomainDN })
    $AclTargets.Add([PSCustomObject]@{ Name="AdminSDHolder"; Type="AdminSDHolder"; DN=$Ctx.AdminSDHolderDN })

    try {
        $AllOUs = @(Get-ADOrganizationalUnit -Server $Domain -Filter * -Properties DistinguishedName,Name -Credential $Credential)
        foreach ($OU in $AllOUs) { $AclTargets.Add([PSCustomObject]@{ Name=$OU.Name; Type="OU"; DN=$OU.DistinguishedName }) }
    } catch {}

    foreach ($PGName in $PrivilegedGroups) {
        try {
            $PG = Get-ADGroup -Server $Domain -Identity $PGName -Credential $Credential
            $AclTargets.Add([PSCustomObject]@{ Name=$PG.Name; Type="Privileged Group"; DN=$PG.DistinguishedName })
        } catch {}
    }

    $LapsGuids = @{}
    foreach ($AttrName in @("ms-Mcs-AdmPwd","ms-Mcs-AdmPwdExpirationTime","msLAPS-Password","msLAPS-EncryptedPassword","msLAPS-PasswordExpirationTime")) {
        $Guid = Get-SchemaAttributeGuid -Server $Domain -LdapDisplayName $AttrName
        if ($Guid) { $LapsGuids[$Guid.ToString().ToLower()] = $AttrName }
    }

    $AclRows = New-Object System.Collections.Generic.List[object]
    foreach ($Target in $AclTargets) {
        $Rules = Get-AclRulesFromDN -Server $Domain -DN $Target.DN
        foreach ($Rule in $Rules) {
            $IdentityName = Convert-SidToName $Rule.IdentityReference
            $RightsText = $Rule.ActiveDirectoryRights.ToString()
            $ObjectType = $Rule.ObjectType.ToString().ToLower()

            $IsBuiltInSafe = $false
            if ($IdentityName -match "SYSTEM" -or $IdentityName -match "Domain Admins" -or $IdentityName -match "Enterprise Admins" -or $IdentityName -match "Administrators" -or $IdentityName -match "ENTERPRISE DOMAIN CONTROLLERS") { $IsBuiltInSafe = $true }

            if ($Target.Type -eq "Domain" -and $DCSyncGuids.ContainsKey($ObjectType)) {
                $AclRows.Add([PSCustomObject]@{ Severity="Critical"; TargetType=$Target.Type; TargetName=$Target.Name; Identity=$IdentityName; Permission=$DCSyncGuids[$ObjectType]; DN=$Target.DN; Finding="DCSync permission detected" })
            }

            if ($ObjectType -eq $ResetPasswordGuid.ToLower() -and -not $IsBuiltInSafe) {
                $AclRows.Add([PSCustomObject]@{ Severity="Medium"; TargetType=$Target.Type; TargetName=$Target.Name; Identity=$IdentityName; Permission="Reset Password"; DN=$Target.DN; Finding="Reset Password delegation detected" })
            }

            foreach ($DangerousRight in $DangerousRights) {
                if ($RightsText -like "*$DangerousRight*" -and -not $IsBuiltInSafe) {
                    $Sev="High"; if ($DangerousRight -in @("GenericAll","WriteDacl","WriteOwner")) { $Sev="Critical" }
                    $AclRows.Add([PSCustomObject]@{ Severity=$Sev; TargetType=$Target.Type; TargetName=$Target.Name; Identity=$IdentityName; Permission=$DangerousRight; DN=$Target.DN; Finding="Dangerous AD permission: $DangerousRight" })
                }
            }

            if ($LapsGuids.ContainsKey($ObjectType) -and -not $IsBuiltInSafe) {
                $AclRows.Add([PSCustomObject]@{ Severity="High"; TargetType=$Target.Type; TargetName=$Target.Name; Identity=$IdentityName; Permission="LAPS attribute: $($LapsGuids[$ObjectType])"; DN=$Target.DN; Finding="Principal has rights over LAPS attribute" })
            }

            if ($IdentityName -match "^S-\d-\d+" -and -not $IsBuiltInSafe) {
                $AclRows.Add([PSCustomObject]@{ Severity="Medium"; TargetType=$Target.Type; TargetName=$Target.Name; Identity=$IdentityName; Permission="Unknown"; DN=$Target.DN; Finding="Unresolved SID in ACL" })
            }

            if ($Target.Type -eq "AdminSDHolder" -and -not $IsBuiltInSafe -and $RightsText -match "GenericAll|WriteDacl|WriteOwner|WriteProperty|ExtendedRight") {
                $AclRows.Add([PSCustomObject]@{ Severity="Critical"; TargetType="AdminSDHolder"; TargetName=$Target.Name; Identity=$IdentityName; Permission=$RightsText; DN=$Target.DN; Finding="Dangerous permission on AdminSDHolder" })
            }
        }
    }

    $Result = [PSCustomObject]@{
        Domain = $Domain
        Vulnerabilities = $AclRows
    }
    
    Write-Output $Result
} catch {
    throw $_
}
