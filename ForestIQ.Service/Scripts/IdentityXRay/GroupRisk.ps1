param(
    [string]$Domain,
    [int]$LargeGroupThreshold = 300
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
    $Groups = @(Get-ADGroup -Server $Domain -Filter * -Properties Description,ManagedBy,WhenCreated,WhenChanged,Members,MemberOf,GroupScope,GroupCategory,DistinguishedName,AdminCount,SIDHistory -Credential $Credential)

    $GroupRisks = New-Object System.Collections.Generic.List[object]

    foreach ($Group in $Groups) {
        $Members = Get-SafeGroupMembers -Server $Domain -Identity $Group.DistinguishedName -Credential $Credential
        $RecursiveMembers = Get-SafeGroupMembers -Server $Domain -Identity $Group.DistinguishedName -Recursive -Credential $Credential
        $DisabledMembers=0; $ServiceMembers=0; $ForeignMembers=0; $UnknownMembers=0

        foreach ($Member in $Members) {
            if ($Member.DistinguishedName -and $Member.DistinguishedName -notlike "*$($Ctx.DistinguishedName)") { $ForeignMembers++ }
            if ($Member.objectClass -eq "foreignSecurityPrincipal") { $ForeignMembers++ }
            if ($Member.objectClass -eq "user") {
                try {
                    $MU = Get-ADUser -Server $Domain -Identity $Member.DistinguishedName -Properties Enabled,Description,ServicePrincipalName -Credential $Credential
                    if (-not $MU.Enabled) { $DisabledMembers++ }
                    if (Test-IsServiceAccount $MU) { $ServiceMembers++ }
                } catch { $UnknownMembers++ }
            }
        }

        $Risks = New-Object System.Collections.Generic.List[string]
        if (@($Members).Count -eq 0 -and $Group.Name -notin @("Domain Guests","Guests")) { $Risks.Add("Empty") }
        if ([string]::IsNullOrWhiteSpace($Group.Description)) { $Risks.Add("NoDescription") }
        if ([string]::IsNullOrWhiteSpace($Group.ManagedBy)) { $Risks.Add("NoOwner") }
        if (@($Members).Count -gt $LargeGroupThreshold) { $Risks.Add("Large") }
        if ($DisabledMembers -gt 0) { $Risks.Add("DisabledMembers") }
        if ($ServiceMembers -gt 0) { $Risks.Add("ServiceMembers") }
        if ($ForeignMembers -gt 0) { $Risks.Add("ForeignMembers") }
        if ($UnknownMembers -gt 0) { $Risks.Add("UnknownMembers") }
        if ($Group.SIDHistory) { $Risks.Add("SIDHistory") }

        $GroupRisks.Add([PSCustomObject]@{
            Name = $Group.Name
            SamAccountName = $Group.SamAccountName
            Scope = $Group.GroupScope
            Category = $Group.GroupCategory
            Description = $Group.Description
            ManagedBy = $Group.ManagedBy
            Created = $Group.WhenCreated
            Changed = $Group.WhenChanged
            DirectMembers = @($Members).Count
            RecursiveMembers = @($RecursiveMembers).Count
            AdminCount = $Group.AdminCount
            Risks = ($Risks -join ", ")
        })
    }

    $Circular = @()
    try { $Circular = @(Test-CircularGroupNesting -Server $Domain -Groups $Groups) } catch { $Circular=@() }
    
    foreach ($Item in $Circular) {
        $GroupRisks.Add([PSCustomObject]@{
            Name = $Item.Group
            Risks = "Circular: $($Item.Path)"
        })
    }

    $Result = [PSCustomObject]@{
        Domain = $Domain
        Groups = $GroupRisks
        CircularNestingDetected = ($Circular.Count -gt 0)
    }
    
    Write-Output $Result
} catch {
    throw $_
}
