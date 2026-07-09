param(
    [string]$Domain,
    [int]$InactiveDays = 90,
    [int]$PasswordAgeWarningDays = 180,
    [int]$PasswordAgeCriticalDays = 365,
    [int]$TooManyGroupsThreshold = 20
)
$ErrorActionPreference = "Stop"

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$Credential = $null
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {    $PrivilegedMemberMap = @{}
    $PrivilegedUserDns = New-Object System.Collections.Generic.HashSet[string]
    $ProtectedUsersMembers = New-Object System.Collections.Generic.HashSet[string]
    
    foreach ($PrivGroupName in $PrivilegedGroups) {
        try {
            $PrivGroup = Get-ADGroup -Server $Domain -Identity $PrivGroupName -Credential $Credential
            $Members = Get-SafeGroupMembers -Server $Domain -Identity $PrivGroup.DistinguishedName -Recursive -Credential $Credential
            foreach ($Member in $Members) {
                if ($Member.objectClass -eq "user") {
                    $PrivilegedUserDns.Add($Member.DistinguishedName) | Out-Null
                    if (-not $PrivilegedMemberMap.ContainsKey($Member.DistinguishedName)) {
                        $PrivilegedMemberMap[$Member.DistinguishedName] = New-Object System.Collections.Generic.List[string]
                    }
                    $PrivilegedMemberMap[$Member.DistinguishedName].Add($PrivGroupName)
                    if ($PrivGroupName -eq "Protected Users") {
                        $ProtectedUsersMembers.Add($Member.DistinguishedName) | Out-Null
                    }
                }
            }
        } catch {}
    }

    $DCs = @(Get-ADDomainController -Server $Domain -Filter * -Credential $Credential)
    $UserProps = @(
        "DisplayName","UserPrincipalName","Enabled","DistinguishedName","Department","Title","Manager","Mail","OfficePhone",
        "Description","WhenCreated","WhenChanged","AccountExpirationDate","LockedOut","LastLogonTimestamp","LastLogonDate",
        "PasswordLastSet","PasswordExpired","PasswordNeverExpires","CannotChangePassword","ServicePrincipalName",
        "UserAccountControl","AdminCount","SIDHistory","MemberOf","LogonCount","msDS-SupportedEncryptionTypes",
        "msDS-AllowedToDelegateTo","msDS-AllowedToActOnBehalfOfOtherIdentity"
    )
    $Users = @(Get-ADUser -Server $Domain -Filter * -Properties $UserProps -Credential $Credential)

    $RealLastLogonMap = @{}
    $UserIndex = 0
    foreach ($User in $Users) {
        $UserIndex++
        $Latest = $null
        $LatestDC = $null
        foreach ($DC in $DCs) {
            try {
                $U = Get-ADUser -Server $DC.HostName -Identity $User.DistinguishedName -Properties lastLogon -Credential $Credential
                if ($U.lastLogon -and $U.lastLogon -gt 0) {
                    $ThisLogon = [DateTime]::FromFileTime($U.lastLogon)
                    if ($null -eq $Latest -or $ThisLogon -gt $Latest) {
                        $Latest = $ThisLogon
                        $LatestDC = $DC.HostName
                    }
                }
            } catch {}
        }
        $RealLastLogonMap[$User.DistinguishedName] = [PSCustomObject]@{ RealLastLogon=$Latest; SourceDC=$LatestDC }
    }

    $UserRisks = New-Object System.Collections.Generic.List[object]
    $RiskSummaryCounts = @{
        Stale = 0; NeverLoggedIn = 0; DisabledWithGroups = 0; PwdNeverExpires = 0; PwdNotRequired = 0;
        OldPassword = 0; NoPreAuth = 0; DES = 0; SPN = 0; Delegation = 0; TempNoExpiry = 0;
    }

    foreach ($User in $Users) {
        $Sam = $User.SamAccountName
        $IsService = Test-IsServiceAccount $User
        $IsPrivileged = $PrivilegedUserDns.Contains($User.DistinguishedName)
        $InProtectedUsers = $ProtectedUsersMembers.Contains($User.DistinguishedName)
        $PasswordAge = Get-AgeDays $User.PasswordLastSet
        $GroupCount = @($User.MemberOf).Count
        if ($GroupCount -gt 0) { $GroupCount++ }

        $RealLastLogon = $null
        $RealLastLogonDC = $null
        if ($RealLastLogonMap.ContainsKey($User.DistinguishedName)) {
            $RealLastLogon = $RealLastLogonMap[$User.DistinguishedName].RealLastLogon
            $RealLastLogonDC = $RealLastLogonMap[$User.DistinguishedName].SourceDC
        }

        $LastForRisk = $User.LastLogonDate
        if ($RealLastLogon) { $LastForRisk = $RealLastLogon }

        $Inactive = $false
        if ($null -eq $LastForRisk) { $Inactive = $true }
        elseif (((Get-Date) - $LastForRisk).TotalDays -ge $InactiveDays) { $Inactive = $true }

        $PrivGroups = @()
        if ($IsPrivileged -and $PrivilegedMemberMap.ContainsKey($User.DistinguishedName)) {
            $PrivGroups = $PrivilegedMemberMap[$User.DistinguishedName]
        }
        
        $SpnCount = 0
        if ($User.ServicePrincipalName) { $SpnCount = $User.ServicePrincipalName.Count }

        $RiskNotes = New-Object System.Collections.Generic.List[string]

        if ($User.Enabled -eq $true -and $Inactive) {
            if ($null -eq $LastForRisk) { $RiskNotes.Add("NeverLoggedIn"); $RiskSummaryCounts["NeverLoggedIn"]++ }
            else { $RiskNotes.Add("Stale"); $RiskSummaryCounts["Stale"]++ }
        }
        if ($User.Enabled -eq $false -and $GroupCount -gt 1) {
            $RiskNotes.Add("DisabledWithGroups"); $RiskSummaryCounts["DisabledWithGroups"]++
        }
        if ($User.Description -like "*temporary*" -and $null -eq $User.AccountExpirationDate) {
            $RiskNotes.Add("TempNoExpiry"); $RiskSummaryCounts["TempNoExpiry"]++
        }
        if ($User.PasswordNeverExpires) {
            $RiskNotes.Add("PwdNeverExpires"); $RiskSummaryCounts["PwdNeverExpires"]++
        }
        if (Get-UacFlag $User.UserAccountControl $UAC_PASSWD_NOTREQD) {
            $RiskNotes.Add("PwdNotRequired"); $RiskSummaryCounts["PwdNotRequired"]++
        }
        if ($null -ne $PasswordAge -and $PasswordAge -ge $PasswordAgeWarningDays) {
            $RiskNotes.Add("OldPassword"); $RiskSummaryCounts["OldPassword"]++
        }
        if (Get-UacFlag $User.UserAccountControl $UAC_DONT_REQ_PREAUTH) {
            $RiskNotes.Add("NoPreAuth"); $RiskSummaryCounts["NoPreAuth"]++
        }
        if (Get-UacFlag $User.UserAccountControl $UAC_USE_DES_KEY_ONLY) {
            $RiskNotes.Add("DES"); $RiskSummaryCounts["DES"]++
        }
        if ($SpnCount -gt 0) {
            $RiskNotes.Add("SPN"); $RiskSummaryCounts["SPN"]++
        }
        if (Get-UacFlag $User.UserAccountControl $UAC_TRUSTED_FOR_DELEGATION) {
            $RiskNotes.Add("Delegation"); $RiskSummaryCounts["Delegation"]++
        }

        $UserRisks.Add([PSCustomObject]@{
            SamAccountName = $Sam
            DisplayName = $User.DisplayName
            UPN = $User.UserPrincipalName
            Enabled = $User.Enabled
            LastLogonDate = $LastForRisk
            RealLastLogonDC = $RealLastLogonDC
            PasswordLastSet = $User.PasswordLastSet
            PasswordAgeDays = $PasswordAge
            PasswordNeverExpires = $User.PasswordNeverExpires
            LockedOut = $User.LockedOut
            GroupCount = $GroupCount
            IsPrivileged = $IsPrivileged
            PrivilegedGroups = ($PrivGroups -join ", ")
            InProtectedUsers = $InProtectedUsers
            IsServiceAccount = $IsService
            SPNCount = $SpnCount
            AdminCount = $User.AdminCount
            Risks = ($RiskNotes -join ", ")
        })
    }

    $Result = [PSCustomObject]@{
        Domain = $Domain
        Users = $UserRisks
        RiskSummary = $RiskSummaryCounts
    }
    
    Write-Output $Result
} catch {
    throw $_
}
