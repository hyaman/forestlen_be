$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = @()
    try {
        $Forest = Get-ADForest -Credential $cred
        if ($ForestFilter -ne 'All' -and -not [string]::IsNullOrEmpty($ForestFilter) -and $Forest.Name -ne $ForestFilter) {
            $Domains = @()
        } else {
            $Domains = if ($DomainFilter -ne 'All' -and -not [string]::IsNullOrEmpty($DomainFilter)) {
                @($DomainFilter)
            } else {
                $Forest.Domains
            }
        }
        foreach ($DomainName in $Domains) {
            $DomainDCs = Get-ADDomainController -Server $DomainName -Credential $cred -Filter *
            if ($SiteFilter -ne 'All' -and -not [string]::IsNullOrEmpty($SiteFilter)) {
                $DomainDCs = $DomainDCs | Where-Object Site -eq $SiteFilter
            }
            if ($DomainDCs) {
                $DCs += $DomainDCs | Select-Object -ExpandProperty HostName
            }
        }
    } catch {
        $DomainDCs = Get-ADDomainController -Credential $cred -Filter *
        if ($SiteFilter -ne 'All' -and -not [string]::IsNullOrEmpty($SiteFilter)) {
            $DomainDCs = $DomainDCs | Where-Object Site -eq $SiteFilter
        }
        $DCs = $DomainDCs | Select-Object -ExpandProperty HostName
    }
} else {
    $DCs = @($TargetDC)
}

# Ensure uniqueness and filter out empty strings before running parallel command
$DCs = $DCs | Select-Object -Unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($DCs.Count -eq 0) {
    return @()
}

# 1. Parallelize Domain Controller collection using Invoke-Command
$InvokeErrors = $null
$results = Invoke-Command -ComputerName $DCs -Credential $cred -ArgumentList $TargetDomain, $cred -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock {
    param($TargetDomain, $PassedCred)
    
    # 4. Make software inventory optional (defaults to true if not provided by caller)
    if ($null -eq $IncludeApplications) { $IncludeApplications = $true }

    $Timings = [System.Collections.Generic.List[object]]::new()
    $sw = [System.Diagnostics.Stopwatch]::new()
    
    # OS / Hardware Collection
    $sw.Restart()
    Import-Module ActiveDirectory -ErrorAction SilentlyContinue
    $Computer = $env:COMPUTERNAME
    
    # 8. Improve CIM/WMI efficiency with a reusable session
    $cimSession = $null
    try { $cimSession = New-CimSession -ErrorAction Stop } catch { }

    $OS = $null; $CPU = $null; $CS = $null; $BIOS = $null
    $CpuLoad = 0; $MemoryUsedPercent = $null; $Platform = "Physical"
    $TargetDC = $Computer
    try {
        if ($cimSession) {
            $OS = Get-CimInstance -CimSession $cimSession -ClassName Win32_OperatingSystem -ErrorAction Stop
            $CPU = Get-CimInstance -CimSession $cimSession -ClassName Win32_Processor -ErrorAction Stop
            $CS = Get-CimInstance -CimSession $cimSession -ClassName Win32_ComputerSystem -ErrorAction Stop
            $BIOS = Get-CimInstance -CimSession $cimSession -ClassName Win32_BIOS -ErrorAction Stop
            
            $TargetDC = if ($CS.Domain) { "$($CS.DNSHostName).$($CS.Domain)" } else { $CS.DNSHostName }

            if ($CPU -is [array]) {
                $CpuLoad = ($CPU | Measure-Object LoadPercentage -Average).Average
            } else {
                $CpuLoad = $CPU.LoadPercentage
            }
            
            if ($OS.TotalVisibleMemorySize -gt 0) {
                $MemoryUsedPercent = [math]::Round((($OS.TotalVisibleMemorySize - $OS.FreePhysicalMemory) / $OS.TotalVisibleMemorySize) * 100, 2)
            }

            if ($CS.Manufacturer -match "VMware") { $Platform = "VMware" }
            elseif ($CS.Manufacturer -match "Microsoft" -and $CS.Model -match "Virtual") { $Platform = "Hyper-V" }
            elseif ($CS.Manufacturer -match "Amazon") { $Platform = "AWS" }
            elseif ($CS.Manufacturer -match "Google") { $Platform = "GCP" }
            elseif ($CS.Manufacturer -match "Microsoft Corporation" -and $CS.Model -match "Virtual Machine") { $Platform = "Azure" }
        }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'OS / Hardware Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Disk Collection
    $sw.Restart()
    $Disks = @()
    try {
        if ($cimSession) {
            $Disks = @(
                Get-CimInstance -CimSession $cimSession -ClassName Win32_LogicalDisk -Filter "DriveType=3" -ErrorAction Stop | ForEach-Object {
                    $FreePercent = if ($_.Size -gt 0) { [math]::Round(($_.FreeSpace / $_.Size) * 100, 2) } else { 0 }
                    [PSCustomObject]@{
                        Drive       = $_.DeviceID
                        VolumeName  = $_.VolumeName
                        FileSystem  = $_.FileSystem
                        SizeGB      = [math]::Round($_.Size / 1GB, 2)
                        FreeGB      = [math]::Round($_.FreeSpace / 1GB, 2)
                        FreePercent = $FreePercent
                        Status      = if ($FreePercent -lt 15) { "WARNING" } else { "OK" }
                    }
                }
            )
        }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Disk Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Services Collection
    $sw.Restart()
    $Services = @()
    try {
        $Services = @(
            @("NTDS", "DNS", "DFSR", "Netlogon", "W32Time", "KDC", "ADWS") | ForEach-Object {
                $Svc = Get-Service $_ -ErrorAction SilentlyContinue
                [PSCustomObject]@{
                    ServiceName = $_
                    Status      = if ($Svc) { $Svc.Status.ToString() } else { "Not Found" }
                    StartType   = if ($Svc) { $Svc.StartType.ToString() } else { "Unknown" }
                    Health      = if ($Svc -and $Svc.Status.ToString() -eq "Running") { "OK" } else { "WARNING" }
                }
            }
        )
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Services Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Roles Collection
    $sw.Restart()
    $Roles = @()
    try {
        # 6. Optimize Windows role collection by only requesting DC relevant roles
        $RolesToCheck = @("AD-Domain-Services", "DNS", "FS-DFS", "GPMC", "RSAT")
        $WindowsRoles = Get-WindowsFeature -Name $RolesToCheck -ErrorAction Stop | Where-Object Installed
        $Roles = @(foreach ($Role in $WindowsRoles) {
            [PSCustomObject]@{
                RoleName = $Role.Name
                Display  = $Role.DisplayName
                Status   = if ($Role.Name -match "AD-Domain-Services|DNS|RSAT|GPMC|FS-DFS") { "OK" } else { "INFO" }
            }
        })
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Roles Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Applications Collection
    $sw.Restart()
    $Apps = @()
    if ($IncludeApplications) {
        try {
            $RegistryPaths = @("HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*")
            $Apps = @(foreach ($Path in $RegistryPaths) {
                Get-ItemProperty $Path -ErrorAction SilentlyContinue | Where-Object DisplayName | ForEach-Object {
                    [PSCustomObject]@{
                        SoftwareName = $_.DisplayName
                        Version      = $_.DisplayVersion
                        Publisher    = $_.Publisher
                        InstallDate  = $_.InstallDate
                        Status       = if ($_.Publisher -match "Microsoft") { "OK" } else { "WARNING - Non-Microsoft Application" }
                    }
                }
            })
        } catch { }
    }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Applications Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Firewall Collection
    $sw.Restart()
    $Firewall = @()
    try {
        $Firewall = @(Get-NetFirewallProfile -ErrorAction Stop | ForEach-Object {
            [PSCustomObject]@{
                Profile = $_.Name
                Enabled = $_.Enabled
                Status  = if ($_.Enabled) { "OK" } else { "WARNING - Firewall Disabled" }
            }
        })
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Firewall Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # NTDS Information Collection
    $sw.Restart()
    $NTDSDB = $null; $NTDSLogPath = $null; $NTDSSizeGB = $null
    try {
        $NTDSParams = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\NTDS\Parameters" -ErrorAction Stop
        $NTDSDB = $NTDSParams."DSA Database file"
        $NTDSLogPath = $NTDSParams."Database log files path"
        if ($NTDSDB -and (Test-Path $NTDSDB)) { $NTDSSizeGB = [math]::Round((Get-Item $NTDSDB).Length / 1GB, 2) }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'NTDS Information Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Patch Collection & Recent Updates Collection
    $sw.Restart()
    $LatestHotfix = $null; $DaysSincePatch = $null; $RecentHotfixes = @()
    try {
        # 3. Eliminate duplicate HotFix queries - Query once and reuse
        $AllHotfixes = @(Get-HotFix -ErrorAction Stop | Where-Object InstalledOn | Sort-Object InstalledOn -Descending)
        if ($AllHotfixes.Count -gt 0) {
            $LatestHotfix = $AllHotfixes[0]
            $DaysSincePatch = [math]::Round(((Get-Date) - $LatestHotfix.InstalledOn).TotalDays, 0)
            
            $RecentHotfixes = @($AllHotfixes | Select-Object -First 10 | ForEach-Object {
                [PSCustomObject]@{
                    HotFixID    = $_.HotFixID
                    InstalledOn = $_.InstalledOn
                    Description = $_.Description
                }
            })
        }
        
        $PendingReboot = ((Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") -or (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") -or (Test-Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations"))

        $PendingUpdateCount = "Unable to check"
        try {
            $UpdateSession = New-Object -ComObject Microsoft.Update.Session
            $Searcher = $UpdateSession.CreateUpdateSearcher()
            $PendingUpdates = $Searcher.Search("IsInstalled=0 and Type='Software'").Updates
            $PendingUpdateCount = $PendingUpdates.Count
        } catch { 
        }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Patch Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })
    $Timings.Add([pscustomobject]@{ Section = 'Recent Updates Collection'; ElapsedMilliseconds = 0 })

    # Defender Collection
    $sw.Restart()
    $DefenderEnabled = "Unable to check"
    $DefenderSigAge = "Unable to check"
    $DefenderVersion = $null
    $DefenderEngine = $null
    try {
        $Defender = Get-MpComputerStatus -ErrorAction Stop
        $DefenderEnabled = $Defender.RealTimeProtectionEnabled
        $DefenderSigAge = $Defender.AntivirusSignatureAge
        $DefenderVersion = $Defender.AntivirusSignatureVersion
        $DefenderEngine = $Defender.AMEngineVersion
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Defender Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # DC Information Collection
    $sw.Restart()
    $DCInfo = $null
    try {
        $DCInfo = Get-ADDomainController -Identity $Computer -Credential $PassedCred -ErrorAction Stop
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'DC Information Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Network Collection
    $sw.Restart()
    $NetworkAdapters = @()
    try {
        $NetworkAdapters = @(Get-NetAdapter -ErrorAction Stop | ForEach-Object {
            $IPInfo = Get-NetIPAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1
            [PSCustomObject]@{
                Name        = $_.Name
                Description = $_.InterfaceDescription
                Status      = $_.Status
                MacAddress  = $_.MacAddress
                LinkSpeed   = $_.LinkSpeed
                IPv4        = $IPInfo.IPAddress
            }
        })
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Network Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # DNS Collection
    $sw.Restart()
    $DnsServers = @()
    try {
        $DnsServers = @(Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction Stop | ForEach-Object {
            [PSCustomObject]@{
                InterfaceAlias = $_.InterfaceAlias
                DnsServers     = ($_.ServerAddresses -join ", ")
            }
        })
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'DNS Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # FSMO Collection
    $sw.Restart()
    $OwnedFSMORoles = @()
    try {
        if ($DCInfo) { $OwnedFSMORoles = $DCInfo.OperationMasterRoles }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'FSMO Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Certificate Collection
    $sw.Restart()
    $Certificates = @()
    try {
        $Certificates = @(Get-ChildItem Cert:\LocalMachine\My -ErrorAction Stop | ForEach-Object {
            [PSCustomObject]@{
                Subject     = $_.Subject
                Issuer      = $_.Issuer
                Thumbprint  = $_.Thumbprint
                NotAfter    = $_.NotAfter
                DaysToExpiry = [math]::Round(($_.NotAfter - (Get-Date)).TotalDays,0)
            }
        })
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Certificate Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Top Directories Collection
    $sw.Restart()
    $TopDirectories = @()
    try {
        $fso = New-Object -ComObject Scripting.FileSystemObject
        $foldersToMeasure = @()
        if ($cimSession) {
            $localDrives = Get-CimInstance -CimSession $cimSession -ClassName Win32_LogicalDisk -Filter "DriveType=3" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty DeviceID
            foreach ($drive in $localDrives) {
                $foldersToMeasure += Get-ChildItem "$drive\" -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
            }
        } else {
            $foldersToMeasure = Get-ChildItem "C:\" -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
        }

        $dirSizes = @()
        foreach ($folder in $foldersToMeasure) {
            $size = 0
            try {
                $size = $fso.GetFolder($folder).Size
            } catch { }
            if ($size -gt 0) {
                $dirSizes += [PSCustomObject]@{
                    Path   = $folder
                    SizeGB = [math]::Round($size / 1GB, 2)
                }
            }
        }
        $TopDirectories = @($dirSizes | Sort-Object SizeGB -Descending | Select-Object -First 5)
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Top Directories Collection'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Removed Event Log Collection as requested (NTDS events are fetched via Get-DcNtdsHealth API)

    # 10. AD Specific Health Checks
    $SysvolStatus = "Unknown"
    $LdapSigning = "Unknown"
    $SmbSigning = "Unknown"
    $ADWSStatus = "Unknown"
    $W32TimeHealth = "Unknown"
    try {
        if ($cimSession) {
            $dfsr = Get-CimInstance -CimSession $cimSession -Namespace root\microsoftdfs -ClassName DfsrReplicatedFolderInfo -ErrorAction SilentlyContinue | Where-Object ReplicationGroupName -match "Domain System Volume"
            if ($dfsr) {
                $SysvolStatus = switch ($dfsr.State) { 0 { "Uninitialized" }; 1 { "Initialized" }; 2 { "Initial Sync" }; 3 { "Auto Recovery" }; 4 { "Normal" }; 5 { "In Error" }; default { "Unknown" } }
            } else {
                $frs = Get-Service ntfrs -ErrorAction SilentlyContinue
                if ($frs -and $frs.Status -eq 'Running') { $SysvolStatus = "FRS Running" } else { $SysvolStatus = "Not Found" }
            }
        }
        
        try {
            $ldapVal = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\NTDS\Parameters" -Name "LDAPServerIntegrity" -ErrorAction Stop).LDAPServerIntegrity
            $LdapSigning = if ($ldapVal -eq 2) { "Required" } elseif ($ldapVal -eq 1) { "Negotiated" } else { "None" }
        } catch { }

        try {
            $smbVal = (Get-ItemProperty "HKLM:\System\CurrentControlSet\Services\LanManServer\Parameters" -Name "RequireSecuritySignature" -ErrorAction Stop).RequireSecuritySignature
            $SmbSigning = if ($smbVal -eq 1) { "Required" } else { "Disabled" }
        } catch { }

        $adwsSvc = $Services | Where-Object ServiceName -eq "ADWS"
        if ($adwsSvc) { $ADWSStatus = $adwsSvc.Status }

        $w32Svc = $Services | Where-Object ServiceName -eq "W32Time"
        if ($w32Svc) { $W32TimeHealth = $w32Svc.Status }
    } catch { }

    # Port Connectivity Tests
    $sw.Restart()
    $PortConnectivity = @()
    try {
        $CriticalPorts = [ordered]@{ DNS = 53; Kerberos = 88; RPC = 135; LDAP = 389; SMB = 445; LDAPS = 636; GC = 3268; GCSSL = 3269; ADWS = 9389; WinRM = 5985 }
        $tasks = [System.Collections.Generic.List[object]]::new()
        
        foreach ($port in $CriticalPorts.GetEnumerator()) {
            $tcp = [System.Net.Sockets.TcpClient]::new()
            $task = $tcp.ConnectAsync($TargetDC, $port.Value)
            $tasks.Add(@{ Tcp = $tcp; Task = $task; Service = $port.Key; Port = $port.Value })
        }
        
        [System.Threading.Tasks.Task[]]$allTasks = $tasks | ForEach-Object { $_.Task }
        $deadline = [System.Threading.Tasks.Task]::WhenAll($allTasks)
        [void]$deadline.Wait(500)
        
        $PortConnectivity = @(foreach ($t in $tasks) {
            $open = $t.Task.Status -eq [System.Threading.Tasks.TaskStatus]::RanToCompletion -and $t.Tcp.Connected
            [PSCustomObject]@{
                ServerName = $Computer
                Service    = $t.Service
                Port       = $t.Port
                Open       = $open
                Status     = if ($open) { "OK" } else { "WARNING - Closed / Blocked" }
            }
        })
        foreach ($t in $tasks) { try { $t.Tcp.Close() } catch {} }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Port Connectivity Tests'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    # Overall Status Calculation
    $sw.Restart()
    $OverallStatus = "OK"
    try {
        $OverallStatus = if (
            $CpuLoad -gt 85 -or
            ($MemoryUsedPercent -ne $null -and $MemoryUsedPercent -gt 90) -or
            $PendingReboot -eq $true -or
            ($DaysSincePatch -ne $null -and $DaysSincePatch -gt 45) -or
            (($Disks | Where-Object Status -eq "WARNING").Count -gt 0) -or
            (($Services | Where-Object Health -eq "WARNING").Count -gt 0)
        ) { "WARNING" } else { "OK" }
    } catch { }
    $sw.Stop()
    $Timings.Add([pscustomobject]@{ Section = 'Overall Status Calculation'; ElapsedMilliseconds = $sw.ElapsedMilliseconds })

    if ($cimSession) {
        Remove-CimSession $cimSession -ErrorAction SilentlyContinue
    }

    $Inventory = [PSCustomObject]@{
        ServerName              = $Computer
        FQDN                    = $TargetDC
        Site                    = if ($DCInfo) { $DCInfo.Site } else { "Unknown" }
        IPv4                    = if ($DCInfo) { $DCInfo.IPv4Address } else { "Unknown" }
        IsGlobalCatalog         = if ($DCInfo) { $DCInfo.IsGlobalCatalog } else { $null }
        IsReadOnlyDC            = if ($DCInfo) { $DCInfo.IsReadOnly } else { $null }
        OperatingSystem         = if ($OS) { $OS.Caption } else { "Unknown" }
        OSVersion               = if ($OS) { $OS.Version } else { "Unknown" }
        Build                   = if ($OS) { $OS.BuildNumber } else { "Unknown" }
        Manufacturer            = if ($CS) { $CS.Manufacturer } else { "Unknown" }
        Model                   = if ($CS) { $CS.Model } else { "Unknown" }
        BIOS                    = if ($BIOS) { $BIOS.SMBIOSBIOSVersion } else { "Unknown" }
        CPU                     = if ($CPU) { ($CPU.Name -join ", ") } else { "Unknown" }
        CPUCores                = if ($CPU) { ($CPU | Measure-Object NumberOfCores -Sum).Sum } else { $null }
        LogicalProcessors       = if ($CPU) { ($CPU | Measure-Object NumberOfLogicalProcessors -Sum).Sum } else { $null }
        CPULoadPercent          = [math]::Round($CpuLoad, 2)
        TotalMemoryGB           = if ($OS) { [math]::Round($OS.TotalVisibleMemorySize / 1MB, 2) } else { $null }
        FreeMemoryGB            = if ($OS) { [math]::Round($OS.FreePhysicalMemory / 1MB, 2) } else { $null }
        MemoryUsedPercent       = $MemoryUsedPercent
        LastBoot                = if ($OS) { $OS.LastBootUpTime } else { $null }
        UptimeDays              = if ($OS) { [math]::Round(((Get-Date) - $OS.LastBootUpTime).TotalDays, 2) } else { $null }
        NTDSDatabasePath        = $NTDSDB
        NTDSLogPath             = $NTDSLogPath
        NTDSSizeGB              = $NTDSSizeGB
        LatestHotfix            = if ($LatestHotfix) { $LatestHotfix.HotFixID } else { "Unknown" }
        LatestHotfixDate        = if ($LatestHotfix) { $LatestHotfix.InstalledOn } else { "Unknown" }
        DaysSinceLastPatch      = $DaysSincePatch
        PendingReboot           = $PendingReboot
        DefenderEnabled         = $DefenderEnabled
        DefenderSigAge          = $DefenderSigAge
        DefenderVersion         = $DefenderVersion
        DefenderEngineVersion   = $DefenderEngine
        NetworkAdapters         = $NetworkAdapters
        DnsServers              = $DnsServers
        Certificates            = $Certificates
        RecentHotfixes          = $RecentHotfixes
        FSMORoles               = $OwnedFSMORoles
        Platform                = $Platform
        OverallStatus           = $OverallStatus
        PendingUpdates          = $PendingUpdateCount
        SysvolReplicationStatus = $SysvolStatus
        LdapSigning             = $LdapSigning
        SmbSigning              = $SmbSigning
        ADWSStatus              = $ADWSStatus
        W32TimeStatus           = $W32TimeHealth
    }

    # 5. Reduce payload size - Add lightweight summary information
    $HealthSummary = [PSCustomObject]@{
        DiskHealth        = if (($Disks | Where-Object Status -eq "WARNING").Count -gt 0) { "WARNING" } else { "OK" }
        ServiceHealth     = if (($Services | Where-Object Health -eq "WARNING").Count -gt 0) { "WARNING" } else { "OK" }
        CertificateHealth = if (($Certificates | Where-Object DaysToExpiry -lt 30).Count -gt 0) { "WARNING" } else { "OK" }
        PortHealth        = if (($PortConnectivity | Where-Object Status -match "WARNING").Count -gt 0) { "WARNING" } else { "OK" }
    }

    return [pscustomobject]@{
        Inventory = $Inventory
        Disks = $Disks
        Services = $Services
        Roles = $Roles
        Apps = $Apps
        Firewall = $Firewall
        PortConnectivity = $PortConnectivity
        HealthSummary = $HealthSummary
        PerformanceMetrics = $Timings
        TopDirectories = $TopDirectories
    }
}

if ($InvokeErrors) {
    foreach ($err in $InvokeErrors) {
        if ($err.CategoryInfo.TargetName) {
            $TargetName = $err.CategoryInfo.TargetName
            $results += [PSCustomObject]@{
                Inventory = [PSCustomObject]@{
                    ServerName = $TargetName
                    FQDN       = $TargetName
                }
                Error = "Failed to connect to DC via WinRM: $($err.Exception.Message)"
                HealthSummary = [PSCustomObject]@{
                    DiskHealth        = "ERROR"
                    ServiceHealth     = "ERROR"
                    CertificateHealth = "ERROR"
                    PortHealth        = "ERROR"
                }
            }
        }
    }
}

# Return clean results array without PSComputerName clutter
$cleanResults = @()
if ($results) {
    foreach ($res in $results) {
        $cleanResults += [PSCustomObject]@{
            Inventory          = $res.Inventory
            Disks              = $res.Disks
            Services           = $res.Services
            Roles              = $res.Roles
            Apps               = $res.Apps
            Firewall           = $res.Firewall
            PortConnectivity   = $res.PortConnectivity
            HealthSummary      = $res.HealthSummary
            PerformanceMetrics = $res.PerformanceMetrics
            TopDirectories     = $res.TopDirectories
        }
    }
}

return [PSCustomObject]@{
    GeneratedAt      = Get-Date
    InventoryResults = $cleanResults
}

