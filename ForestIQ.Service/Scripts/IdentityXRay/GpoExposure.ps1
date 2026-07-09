param(
    [string]$Domain
)
$ErrorActionPreference = "Stop"

try {
    $GroupPolicyAvailable = $false
    try {
        Import-Module GroupPolicy -ErrorAction Stop
        $GroupPolicyAvailable = $true
    } catch {
        $GroupPolicyAvailable = $false
    }

    if (-not $GroupPolicyAvailable) {
        $Result = [PSCustomObject]@{
            Domain = $Domain
            ModuleAvailable = $false
            VulnerableGPOs = @()
        }
        Write-Output $Result
        return
    }

    # Get-GPO doesn't support -Credential, so we rely on the implicit PS Remoting context or a double-hop execution if needed
    $Gpos = @(Get-GPO -Domain $Domain -All)
    $GpoRows = New-Object System.Collections.Generic.List[object]

    foreach ($Gpo in $Gpos) {
        $XmlText = $null
        try { $XmlText = Get-GPOReport -Domain $Domain -Guid $Gpo.Id -ReportType Xml } catch { continue }
        $Indicators = @()
        if ($XmlText -match "RestrictedGroups") { $Indicators += "RestrictedGroups" }
        if ($XmlText -match "LocalUsersAndGroups") { $Indicators += "LocalUsersAndGroups" }
        if ($XmlText -match "Administrators") { $Indicators += "Administrators" }
        if ($XmlText -match "Remote Desktop Users") { $Indicators += "Remote Desktop Users" }
        if ($XmlText -match "Groups.xml") { $Indicators += "Groups.xml" }
        if ($XmlText -match "ForestLen\\IdentityXRay") { $Indicators += "ForestLen Marker" }

        if ($Indicators.Count -gt 0) {
            $GpoRows.Add([PSCustomObject]@{ 
                Name = $Gpo.DisplayName
                Id = $Gpo.Id
                Indicators = ($Indicators -join ", ")
                ModificationTime = $Gpo.ModificationTime 
            })
        }
    }

    $Result = [PSCustomObject]@{
        Domain = $Domain
        ModuleAvailable = $true
        VulnerableGPOs = $GpoRows
    }
    
    Write-Output $Result
} catch {
    throw $_
}
