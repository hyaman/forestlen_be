try {
    Import-Module GroupPolicy -ErrorAction Stop
    $Script:GroupPolicyAvailable = $true
} catch {
    $Script:GroupPolicyAvailable = $false
}

$Script:Findings = New-Object System.Collections.Generic.List[object]
$Script:CheckResults = New-Object System.Collections.Generic.List[object]
$Script:DomainSummaries = New-Object System.Collections.Generic.List[object]
$Script:AllUsers = New-Object System.Collections.Generic.List[object]
$Script:AllGroups = New-Object System.Collections.Generic.List[object]
$Script:AllComputers = New-Object System.Collections.Generic.List[object]
$Script:AllSpns = New-Object System.Collections.Generic.List[object]
$Script:AllLogonEvents = New-Object System.Collections.Generic.List[object]
$Script:AllTrusts = New-Object System.Collections.Generic.List[object]
$Script:AllFgpp = New-Object System.Collections.Generic.List[object]
$Script:SchemaCache = @{}

# ============================================================
# Display Helpers
# ============================================================

function Write-Banner {
    param([string]$Text, [string]$Color = "Cyan")
    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor $Color
    Write-Host " $Text" -ForegroundColor $Color
    Write-Host "================================================================================" -ForegroundColor $Color
}

function Write-Step {
    param([string]$Text)
    Write-Host ""
    Write-Host ">>> $Text" -ForegroundColor Yellow
}

function Write-TestLine {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Details = ""
    )
    $Color = "White"
    switch ($Status) {
        "PASSED" { $Color = "Green" }
        "FAILED" { $Color = "Red" }
        "WARNING" { $Color = "Yellow" }
        "SKIPPED" { $Color = "DarkYellow" }
        "INFO" { $Color = "Gray" }
        default { $Color = "White" }
    }
    Write-Host ("[{0}] {1} {2}" -f $Status.PadRight(7), $Name, $Details) -ForegroundColor $Color
}

function Add-CheckResult {
    param(
        [string]$Domain,
        [string]$Section,
        [string]$Status,
        [string]$Details
    )
    $Script:CheckResults.Add([PSCustomObject]@{
        Domain = $Domain
        Section = $Section
        Status = $Status
        Details = $Details
    })
    Write-TestLine -Name "$Domain | $Section" -Status $Status.ToUpper() -Details $Details
}

function Show-Table {
    param(
        [array]$Data,
        [string]$EmptyMessage = "No rows.",
        [int]$MaxRows = $MaxRowsPerSection
    )
    if ($ShowAllFindings) { $MaxRows = 1000000 }

    if ($null -eq $Data -or @($Data).Count -eq 0) {
        Write-Host $EmptyMessage -ForegroundColor Green
        return
    }

    $Count = @($Data).Count
    if ($Count -gt $MaxRows) {
        Write-Host "Showing first $MaxRows of $Count rows. Use -ShowAllFindings to print everything." -ForegroundColor DarkYellow
        $Data | Select-Object -First $MaxRows | Format-Table -Wrap -AutoSize
    } else {
        $Data | Format-Table -Wrap -AutoSize
    }
}


function Write-TestEvidence {
    param(
        [string]$Text,
        [string]$Color = "Gray"
    )
    Write-Host ("         {0}" -f $Text) -ForegroundColor $Color
}

function Show-SampleTable {
    param(
        [array]$Data,
        [string]$Title,
        [string]$EmptyMessage,
        [int]$MaxRows = $SampleRowsPerSection
    )
    Write-Host ("         Evidence: {0}" -f $Title) -ForegroundColor Gray
    if ($null -eq $Data -or @($Data).Count -eq 0) {
        Write-Host ("         {0}" -f $EmptyMessage) -ForegroundColor Green
        return
    }
    $Count = @($Data).Count
    if ($Count -gt $MaxRows) {
        Write-Host ("         Showing first {0} of {1} rows." -f $MaxRows, $Count) -ForegroundColor DarkYellow
        $Data | Select-Object -First $MaxRows | Format-Table -Wrap -AutoSize
    } else {
        $Data | Format-Table -Wrap -AutoSize
    }
}

function Add-Finding {
    param(
        [string]$Domain,
        [string]$Severity,
        [string]$Category,
        [string]$ObjectType,
        [string]$ObjectName,
        [string]$Finding,
        [string]$Evidence,
        [string]$Recommendation,
        [int]$Score = 0
    )

    if ($Score -eq 0) {
        switch ($Severity) {
            "Critical" { $Score = 100 }
            "High"     { $Score = 70 }
            "Medium"   { $Score = 40 }
            "Low"      { $Score = 15 }
            "Info"     { $Score = 5 }
            default    { $Score = 1 }
        }
    }

    $Script:Findings.Add([PSCustomObject]@{
        Domain = $Domain
        Severity = $Severity
        Category = $Category
        ObjectType = $ObjectType
        ObjectName = $ObjectName
        Finding = $Finding
        Evidence = $Evidence
        Recommendation = $Recommendation
        Score = $Score
    })
}

# ============================================================
# Utility Helpers
# ============================================================

function Get-DomainPrefix {
    param([string]$DomainName)
    if ($DomainName -eq "slv.org") { return "slv" }
    return ($DomainName.Split(".")[0]).ToLower()
}

function Get-DomainContext {
    param([string]$DomainName)
    $Domain = Get-ADDomain -Server $DomainName -ErrorAction Stop
    $Prefix = Get-DomainPrefix -DomainName $Domain.DNSRoot
    return [PSCustomObject]@{
        DNSRoot = $Domain.DNSRoot
        DistinguishedName = $Domain.DistinguishedName
        NetBIOSName = $Domain.NetBIOSName
        Prefix = $Prefix
        AdminSDHolderDN = "CN=AdminSDHolder,CN=System,$($Domain.DistinguishedName)"
    }
}

function Test-SchemaAttribute {
    param(
        [string]$Server,
        [string]$LdapDisplayName
    )

    $Key = "$Server|$LdapDisplayName"
    if ($Script:SchemaCache.ContainsKey($Key)) {
        return $Script:SchemaCache[$Key].Exists
    }

    try {
        $Root = Get-ADRootDSE -Server $Server -ErrorAction Stop
        $Attr = Get-ADObject -Server $Server `
            -SearchBase $Root.SchemaNamingContext `
            -LDAPFilter "(&(objectClass=attributeSchema)(lDAPDisplayName=$LdapDisplayName))" `
            -Properties lDAPDisplayName,schemaIDGUID `
            -ErrorAction SilentlyContinue

        $Exists = ($null -ne $Attr)
        $Guid = $null
        if ($Attr -and $Attr.schemaIDGUID) {
            $Guid = [Guid]$Attr.schemaIDGUID
        }

        $Script:SchemaCache[$Key] = [PSCustomObject]@{
            Exists = $Exists
            Guid = $Guid
        }

        return $Exists
    } catch {
        $Script:SchemaCache[$Key] = [PSCustomObject]@{
            Exists = $false
            Guid = $null
        }
        return $false
    }
}

function Get-SchemaAttributeGuid {
    param(
        [string]$Server,
        [string]$LdapDisplayName
    )
    $null = Test-SchemaAttribute -Server $Server -LdapDisplayName $LdapDisplayName
    $Key = "$Server|$LdapDisplayName"
    return $Script:SchemaCache[$Key].Guid
}

function Get-UacFlag {
    param([int]$UserAccountControl, [int]$Flag)
    return (($UserAccountControl -band $Flag) -ne 0)
}

function Get-AgeDays {
    param($DateValue)
    if ($null -eq $DateValue) { return $null }
    try { return [int]((Get-Date) - $DateValue).TotalDays } catch { return $null }
}

function Test-IsServiceAccount {
    param($User)
    $Sam = ""
    $Desc = ""
    $Title = ""
    if ($User.SamAccountName) { $Sam = $User.SamAccountName.ToLower() }
    if ($User.Description) { $Desc = $User.Description.ToLower() }
    if ($User.Title) { $Title = $User.Title.ToLower() }

    if ($Sam -like "svc.*" -or $Sam -like "svc_*" -or
        $Sam -like "sa.*" -or $Sam -like "sa_*" -or
        $Sam -like "*service*" -or
        $Desc -like "*service*" -or $Desc -like "*sql*" -or
        $Desc -like "*backup*" -or $Desc -like "*application*" -or
        $Title -like "*service*" -or
        ($User.ServicePrincipalName -and $User.ServicePrincipalName.Count -gt 0)) {
        return $true
    }
    return $false
}

function Test-IsBusinessHours {
    param([datetime]$Time)
    try {
        $Start = [TimeSpan]::Parse($BusinessHoursStart)
        $End = [TimeSpan]::Parse($BusinessHoursEnd)
        if ($Time.DayOfWeek -in @("Saturday","Sunday")) { return $false }
        if ($Time.TimeOfDay -lt $Start -or $Time.TimeOfDay -gt $End) { return $false }
        return $true
    } catch {
        return $true
    }
}

function Get-EventDataValue {
    param([xml]$EventXml, [string]$Name)
    $Node = $EventXml.Event.EventData.Data | Where-Object { $_.Name -eq $Name } | Select-Object -First 1
    if ($Node) { return [string]$Node.'#text' }
    return $null
}

function Get-SafeGroupMembers {
    param([string]$Server, [string]$Identity, [switch]$Recursive)
    try {
        if ($Recursive) { return @(Get-ADGroupMember -Server $Server -Identity $Identity -Recursive -ErrorAction Stop) }
        return @(Get-ADGroupMember -Server $Server -Identity $Identity -ErrorAction Stop)
    } catch { return @() }
}

function Get-SafeUserGroups {
    param([string]$Server, [string]$UserDN)
    try { return @(Get-ADPrincipalGroupMembership -Server $Server -Identity $UserDN -ErrorAction Stop) }
    catch { return @() }
}

function Convert-SidToName {
    param([System.Security.Principal.SecurityIdentifier]$Sid)
    try { return $Sid.Translate([System.Security.Principal.NTAccount]).Value }
    catch { return $Sid.Value }
}

function Get-AclRulesFromDN {
    param([string]$Server, [string]$DN)
    try {
        $Entry = New-Object System.DirectoryServices.DirectoryEntry("LDAP://$Server/$DN")
        $Security = $Entry.ObjectSecurity
        return @($Security.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier]))
    } catch { return @() }
}

function Resolve-GroupPathToTarget {
    param([string]$Server, [string]$StartUserDN, [string]$TargetGroupDN)

    $Visited = @{}
    $Queue = New-Object System.Collections.Queue
    try {
        $User = Get-ADUser -Server $Server -Identity $StartUserDN -Properties MemberOf -ErrorAction Stop
    } catch { return $null }

    if (-not $User.MemberOf) { return $null }

    foreach ($G in $User.MemberOf) {
        $Queue.Enqueue([PSCustomObject]@{ DN=$G; Path=@($User.SamAccountName,$G) })
    }

    while ($Queue.Count -gt 0) {
        $Item = $Queue.Dequeue()
        if ($Visited.ContainsKey($Item.DN)) { continue }
        $Visited[$Item.DN] = $true

        if ($Item.DN -eq $TargetGroupDN) {
            $Readable = @()
            foreach ($DN in $Item.Path) {
                if ($DN -like "CN=*" -or $DN -like "OU=*") {
                    try {
                        $Obj = Get-ADObject -Server $Server -Identity $DN -Properties Name -ErrorAction Stop
                        $Readable += $Obj.Name
                    } catch { $Readable += $DN }
                } else { $Readable += $DN }
            }
            return ($Readable -join " -> ")
        }

        try {
            $Group = Get-ADGroup -Server $Server -Identity $Item.DN -Properties MemberOf -ErrorAction Stop
            foreach ($Parent in $Group.MemberOf) {
                $NewPath = @()
                $NewPath += $Item.Path
                $NewPath += $Parent
                $Queue.Enqueue([PSCustomObject]@{ DN=$Parent; Path=$NewPath })
            }
        } catch {}
    }
    return $null
}

function Test-CircularGroupNesting {
    param([string]$Server, [array]$Groups)
    $Out = @()
    foreach ($Group in $Groups) {
        $Visited = @{}
        $Stack = New-Object System.Collections.Stack
        $Stack.Push([PSCustomObject]@{ DN=$Group.DistinguishedName; Path=@($Group.Name) })
        while ($Stack.Count -gt 0) {
            $Current = $Stack.Pop()
            if ($Visited.ContainsKey($Current.DN)) { continue }
            $Visited[$Current.DN] = $true
            $Members = Get-SafeGroupMembers -Server $Server -Identity $Current.DN
            foreach ($Member in $Members) {
                if ($Member.objectClass -eq "group") {
                    if ($Member.DistinguishedName -eq $Group.DistinguishedName) {
                        $Out += [PSCustomObject]@{ Group=$Group.Name; Path=(($Current.Path + $Member.Name) -join " -> ") }
                    } else {
                        $Stack.Push([PSCustomObject]@{ DN=$Member.DistinguishedName; Path=($Current.Path + $Member.Name) })
                    }
                }
            }
        }
    }
    return $Out
}

# ============================================================
# Constants
# ============================================================

$UAC_PASSWD_NOTREQD = 0x0020
$UAC_DONT_EXPIRE_PASSWORD = 0x10000
$UAC_TRUSTED_FOR_DELEGATION = 0x80000
$UAC_USE_DES_KEY_ONLY = 0x200000
$UAC_DONT_REQ_PREAUTH = 0x400000
$UAC_TRUSTED_TO_AUTH_FOR_DELEGATION = 0x1000000

$PrivilegedGroups = @(
    "Domain Admins","Enterprise Admins","Schema Admins","Administrators",
    "Account Operators","Server Operators","Backup Operators","Print Operators",
    "DnsAdmins","Group Policy Creator Owners","Remote Desktop Users",
    "Remote Management Users","Cert Publishers","Key Admins","Enterprise Key Admins",
    "Protected Users"
)

$DangerousRights = @("GenericAll","GenericWrite","WriteDacl","WriteOwner","AllExtendedRights","ExtendedRight","WriteProperty","CreateChild","DeleteChild","DeleteTree")

$DCSyncGuids = @{
    "1131f6aa-9c07-11d1-f79f-00c04fc2dcd2" = "Replicating Directory Changes"
    "1131f6ad-9c07-11d1-f79f-00c04fc2dcd2" = "Replicating Directory Changes All"
    "89e95b76-444d-4c62-991a-0facbeda640c" = "Replicating Directory Changes In Filtered Set"
}

$ResetPasswordGuid = "00299570-246d-11d0-a768-00aa006e0529"

$ENC_DES_CRC = 0x1
$ENC_DES_MD5 = 0x2
$ENC_RC4 = 0x4
$ENC_AES128 = 0x8
$ENC_AES256 = 0x10

# ============================================================
