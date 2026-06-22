Import-Module ActiveDirectory -ErrorAction Stop

$report = [pscustomobject][ordered]@{
    GeneratedAt         = Get-Date
    DomainForestInfo    = $null
    ForestInfo          = $null
    Domains             = @()
    Sites               = @()
    SiteLinkReport      = @()
    Subnets             = @()
    DomainControllers   = @()
    SiteSubnetDCMapping = @()
    DcLocator           = $null
    TopLevelOUs         = @()
    SecurityPosture     = $null
    Errors              = @()
    Timings             = [System.Collections.Generic.List[object]]::new()
}

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

$t0 = Get-Date
try
{
    $domain = Get-ADDomain -Credential $cred -ErrorAction Stop
    $forest = Get-ADForest -Credential $cred -ErrorAction Stop
}
catch
{
    $report.Errors += [pscustomobject]@{ Section = 'Bootstrap'; Error = $_.Exception.Message }
    $report
    return
}
$report.Timings.Add([pscustomobject]@{ Section = 'Bootstrap'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$allSites = @()
$allSubnets = @()

$t0 = Get-Date
try {
    $allSites = @(Get-ADReplicationSite -Filter * -Properties Description -Credential $cred -ErrorAction Stop)
    if ($global:FilterSite) { $allSites = @($allSites | Where-Object { $_.Name -match $global:FilterSite }) }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'Sites'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'Sites'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try {
    $allSubnets = @(Get-ADReplicationSubnet -Filter * -Properties Site,Location,Description -Credential $cred -ErrorAction Stop)
    if ($global:FilterSite) { $allSubnets = @($allSubnets | Where-Object { $_.Site -match $global:FilterSite }) }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'Subnets'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'Subnets'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try
{
    $userCount = 0; $groupCount = 0
    try { $userCount = @(Get-ADUser -LDAPFilter "(objectClass=user)" -ResultSetSize $null -Credential $cred).Count } catch {}
    try { $groupCount = @(Get-ADGroup -LDAPFilter "(objectClass=group)" -ResultSetSize $null -Credential $cred).Count } catch {}

    $report.DomainForestInfo = [pscustomobject]@{
        DomainName           = $domain.DNSRoot
        NetBIOSName          = $domain.NetBIOSName
        DomainMode           = $domain.DomainMode.ToString()
        ForestName           = $forest.Name
        ForestMode           = $forest.ForestMode.ToString()
        UserCount            = $userCount
        GroupCount           = $groupCount
        PDCEmulator          = $domain.PDCEmulator
        RIDMaster            = $domain.RIDMaster
        InfrastructureMaster = $domain.InfrastructureMaster
        SchemaMaster         = $forest.SchemaMaster
        DomainNamingMaster   = $forest.DomainNamingMaster
    }

    $report.ForestInfo = [pscustomobject]@{
        ForestName           = $forest.Name
        RootDomain           = $forest.RootDomain
        Domains              = $forest.Domains
        GlobalCatalogs       = $forest.GlobalCatalogs
        Sites                = $forest.Sites
        ForestMode           = $forest.ForestMode.ToString()
        DomainName           = $domain.DNSRoot
        NetBIOSName          = $domain.NetBIOSName
        DomainMode           = $domain.DomainMode.ToString()
        UserCount            = $userCount
        GroupCount           = $groupCount
        PDCEmulator          = $domain.PDCEmulator
        RIDMaster            = $domain.RIDMaster
        InfrastructureMaster = $domain.InfrastructureMaster
        SchemaMaster         = $forest.SchemaMaster
        DomainNamingMaster   = $forest.DomainNamingMaster
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'DomainForestInfo'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'DomainForestInfo'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try
{
    $report.Domains = foreach ($domainName in $forest.Domains)
    {
        if ($global:FilterDomain -and ($domainName -notmatch $global:FilterDomain)) { continue }
        try
        {
            $domainInfo = Get-ADDomain -Server $domainName -Credential $cred -ErrorAction Stop
            [pscustomobject]@{
                DomainName           = $domainInfo.DNSRoot
                NetBIOSName          = $domainInfo.NetBIOSName
                DomainMode           = $domainInfo.DomainMode.ToString()
                ParentDomain         = if ($domainInfo.ParentDomain) { $domainInfo.ParentDomain } else { 'Root Domain' }
                ChildDomains         = @($domainInfo.ChildDomains)
                PDCEmulator          = $domainInfo.PDCEmulator
                RIDMaster            = $domainInfo.RIDMaster
                InfrastructureMaster = $domainInfo.InfrastructureMaster
                Status               = 'OK'
            }
        }
        catch
        {
            [pscustomobject]@{ DomainName = $domainName; Status = 'ERROR'; Error = $_.Exception.Message; Exception = $_.Exception.GetType().FullName }
        }
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'ForestDiscovery'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'Domains'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$report.Sites = $allSites | Select-Object Name, Description
$report.Subnets = $allSubnets | Select-Object Name, Site, Location, Description

$t0 = Get-Date
try
{
    $report.SiteLinkReport = Get-ADReplicationSiteLink -Filter * -Properties Cost,ReplicationFrequencyInMinutes,SitesIncluded,ReplicationSchedule -Credential $cred -ErrorAction Stop | 
        Where-Object { -not $global:FilterSite -or ($_.SitesIncluded -match $global:FilterSite) } | 
        ForEach-Object {
        [pscustomobject]@{
            SiteLinkName               = $_.Name
            Cost                       = $_.Cost
            ReplicationIntervalMinutes = $_.ReplicationFrequencyInMinutes
            ScheduleExists             = if ($_.ReplicationSchedule) { 'Yes' } else { 'No' }
            ScheduleExistsBool         = [bool]$_.ReplicationSchedule
            SitesIncluded              = ($_.SitesIncluded -join ', ')
            SitesIncludedArray         = @($_.SitesIncluded)
            Status = if ($_.ReplicationFrequencyInMinutes -gt 180) { 'Warning' } elseif ($_.ReplicationSchedule) { 'Warning' } elseif ($_.Cost -eq 100 -and $_.ReplicationFrequencyInMinutes -eq 180 -and -not $_.ReplicationSchedule) { 'Informational' } else { 'Healthy' }
            StatusReason = if ($_.ReplicationFrequencyInMinutes -gt 180) { "Replication interval is $($_.ReplicationFrequencyInMinutes) minutes, which is higher than the default 180 minutes." } elseif ($_.ReplicationSchedule) { 'A custom replication schedule is configured and should be reviewed.' } elseif ($_.Cost -eq 100 -and $_.ReplicationFrequencyInMinutes -eq 180 -and -not $_.ReplicationSchedule) { 'Site link is using default Active Directory settings.' } else { 'Site link configuration appears healthy.' }
        }
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'SiteLinkReport'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'SiteLinkReport'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$dcs = @()
$t0 = Get-Date
try
{
    foreach ($d in $forest.Domains) {
        if ($global:FilterDomain -and ($d -notmatch $global:FilterDomain)) { continue }
        try {
            $domainDCs = @(Get-ADDomainController -Filter * -Server $d -Credential $cred -ErrorAction Stop | Select-Object HostName, Name, Site, IPv4Address, IsGlobalCatalog, IsReadOnly, OperatingSystem, OperatingSystemVersion, @{Name = 'UptimeHours'; Expression = { $uptime = 0; try { $cim = Get-CimInstance -ClassName Win32_OperatingSystem -ComputerName $_.HostName -ErrorAction Stop; $uptime = [math]::Round((Get-Date).Subtract($cim.LastBootUpTime).TotalHours, 1) } catch {}; $uptime }}, OperationMasterRoles)
            if ($global:FilterSite) { $domainDCs = @($domainDCs | Where-Object { $_.Site -match $global:FilterSite }) }
            if ($null -ne $domainDCs -and $domainDCs.Count -gt 0) { $dcs += $domainDCs }
        } catch { }
    }
    $report.DomainControllers = $dcs
}
catch { $report.Errors += [pscustomobject]@{ Section = 'DomainControllers'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'DomainControllers'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try
{
    $report.SiteSubnetDCMapping = foreach ($site in $allSites)
    {
        $siteSubnets = @($allSubnets | Where-Object { $_.Site -like "*CN=$($site.Name),*" -or $_.Site -eq $site.Name })
        $siteDCs = @($dcs | Where-Object { $_.Site -eq $site.Name })
        $subnetNames = @($siteSubnets | Select-Object -ExpandProperty Name)

        if ($siteDCs) { foreach ($dc in $siteDCs) { [pscustomobject]@{ SiteName = $site.Name; Subnets = if ($subnetNames.Count -gt 0) { ($subnetNames -join ', ') } else { 'No subnet assigned' }; SubnetList = $subnetNames; DomainController = $dc.HostName; DCName = $dc.Name; DCIPAddress = $dc.IPv4Address; IsGlobalCatalog = $dc.IsGlobalCatalog; IsReadOnly = $dc.IsReadOnly; OperatingSystem = $dc.OperatingSystem; FSMORoles = if ($dc.OperationMasterRoles) { ($dc.OperationMasterRoles -join ', ') } else { 'None' } } } }
        else { [pscustomobject]@{ SiteName = $site.Name; Subnets = if ($subnetNames.Count -gt 0) { ($subnetNames -join ', ') } else { 'No subnet assigned' }; SubnetList = $subnetNames; DomainController = $null; DCName = $null; DCIPAddress = $null; IsGlobalCatalog = $null; IsReadOnly = $null; OperatingSystem = $null; FSMORoles = 'None' } }
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'SiteSubnetDCMapping'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'SiteSubnetDCMapping'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try
{
    $raw = nltest /dsgetdc:$($domain.DNSRoot)
    $dcLocator = [ordered]@{}
    foreach ($line in $raw) { switch -Regex ($line) { '^DC:\s+(.*)$' { $dcLocator.DomainController = $Matches[1].Trim('\') }; '^Address:\s+(.*)$' { $dcLocator.Address = $Matches[1].Trim('\') }; '^Dom Name:\s+(.*)$' { $dcLocator.DomainName = $Matches[1] }; '^Forest Name:\s+(.*)$' { $dcLocator.ForestName = $Matches[1] }; '^Dc Site Name:\s+(.*)$' { $dcLocator.DcSiteName = $Matches[1] }; '^Our Site Name:\s+(.*)$' { $dcLocator.OurSiteName = $Matches[1] }; '^Flags:\s+(.*)$' { $dcLocator.Flags = $Matches[1] } } }
    $report.DcLocator = [pscustomobject]$dcLocator
}
catch { $report.Errors += [pscustomobject]@{ Section = 'DcLocator'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'DcLocator'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try
{
    $domainDN = (Get-ADDomain -Credential $cred -ErrorAction Stop).DistinguishedName
    $topLevelOUs = @(Get-ADOrganizationalUnit -Filter * -SearchBase $domainDN -SearchScope OneLevel -Credential $cred -ErrorAction Stop)
    $report.TopLevelOUs = foreach ($ou in $topLevelOUs) {
        [pscustomobject]@{
            Name              = $ou.Name
            DistinguishedName = $ou.DistinguishedName
        }
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'TopLevelOUs'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'TopLevelOUs'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$t0 = Get-Date
try
{
    $pwdPolicy = Get-ADDefaultDomainPasswordPolicy -Credential $cred -ErrorAction Stop
    $recycleBin = Get-ADOptionalFeature -Filter {Name -eq "Recycle Bin Feature"} -Credential $cred -ErrorAction Stop
    
    $report.SecurityPosture = [pscustomobject]@{
        RecycleBinEnabled      = ($recycleBin.EnabledScopes.Count -gt 0)
        ComplexityEnabled      = $pwdPolicy.ComplexityEnabled
        MaxPasswordAgeDays     = if ($pwdPolicy.MaxPasswordAge) { $pwdPolicy.MaxPasswordAge.Days } else { $null }
        MinPasswordAgeDays     = if ($pwdPolicy.MinPasswordAge) { $pwdPolicy.MinPasswordAge.Days } else { $null }
        MinPasswordLength      = $pwdPolicy.MinPasswordLength
        PasswordHistoryCount   = $pwdPolicy.PasswordHistoryCount
        LockoutDurationMins    = if ($pwdPolicy.LockoutDuration) { $pwdPolicy.LockoutDuration.TotalMinutes } else { $null }
        LockoutObservationMins = if ($pwdPolicy.LockoutObservationWindow) { $pwdPolicy.LockoutObservationWindow.TotalMinutes } else { $null }
        LockoutThreshold       = $pwdPolicy.LockoutThreshold
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'SecurityPosture'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'SecurityPosture'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$report
