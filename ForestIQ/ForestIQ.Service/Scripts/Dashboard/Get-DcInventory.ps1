$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = Get-ADDomainController -Credential $cred -Filter * | Select-Object -ExpandProperty HostName
} else {
    $DCs = @($TargetDC)
}

$results = @()

foreach ($DCName in $DCs) {
    $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName -ScriptBlock {
        param($TargetDomain, $TargetDC)
        Import-Module ActiveDirectory -ErrorAction SilentlyContinue
        $Computer = $env:COMPUTERNAME

        # OS / Hardware
        $OS = Get-CimInstance Win32_OperatingSystem
        $CPU = Get-CimInstance Win32_Processor
        $CS = Get-CimInstance Win32_ComputerSystem
        $BIOS = Get-CimInstance Win32_BIOS
        $CpuLoad = ($CPU | Measure-Object LoadPercentage -Average).Average
        $MemoryUsedPercent = if ($OS.TotalVisibleMemorySize -gt 0) {
            [math]::Round((($OS.TotalVisibleMemorySize - $OS.FreePhysicalMemory) / $OS.TotalVisibleMemorySize) * 100, 2)
        } else { $null }
        
        $Platform = "Physical"

        if ($CS.Manufacturer -match "VMware") {
            $Platform = "VMware"
        }
        elseif ($CS.Manufacturer -match "Microsoft" -and $CS.Model -match "Virtual") {
            $Platform = "Hyper-V"
        }
        elseif ($CS.Manufacturer -match "Amazon") {
            $Platform = "AWS"
        }
        elseif ($CS.Manufacturer -match "Google") {
            $Platform = "GCP"
        }
        elseif ($CS.Manufacturer -match "Microsoft Corporation" -and $CS.Model -match "Virtual Machine") {
            $Platform = "Azure"
        }

        # Disk
        $Disks = @(
            Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object {

                $FreePercent = if ($_.Size -gt 0) {
                    [math]::Round(($_.FreeSpace / $_.Size) * 100, 2)
                } else {
                    0
                }

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

        # Services
        $Services = @(
            @("NTDS", "DNS", "DFSR", "Netlogon", "W32Time", "KDC", "ADWS") | ForEach-Object {
                $Svc = Get-Service $_ -ErrorAction SilentlyContinue
                [PSCustomObject]@{
                    ServiceName = $_
                    Status      = if ($Svc) { $Svc.Status } else { "Not Found" }
                    StartType   = if ($Svc) { $Svc.StartType } else { "Unknown" }
                    Health      = if ($Svc -and $Svc.Status -eq "Running") { "OK" } else { "WARNING" }
                }
            }
        )

        # Roles
        $Roles = @()
        try {
            $WindowsRoles = Get-WindowsFeature | Where-Object Installed
            $Roles = @(foreach ($Role in $WindowsRoles) {
                [PSCustomObject]@{
                    RoleName = $Role.Name
                    Display  = $Role.DisplayName
                    Status   = if ($Role.Name -match "AD-Domain-Services|DNS|RSAT|GPMC|FS-DFS|WAS|NET-Framework") { "OK" } elseif ($Role.Name -match "Web-|DHCP|Hyper-V|Print|Remote-Desktop") { "WARNING" } else { "INFO" }
                }
            })
        } catch { }

        # Apps
        $Apps = @()
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

        # Firewall
        $Firewall = @()
        try {
            $Firewall = @(Get-NetFirewallProfile | ForEach-Object {
                [PSCustomObject]@{
                    Profile = $_.Name
                    Enabled = $_.Enabled
                    Status  = if ($_.Enabled) { "OK" } else { "WARNING - Firewall Disabled" }
                }
            })
        } catch { }

        # NTDS Info
        $NTDSParams = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\NTDS\Parameters" -ErrorAction SilentlyContinue
        $NTDSDB = $NTDSParams."DSA Database file"
        $NTDSLogPath = $NTDSParams."Database log files path"
        $NTDSSizeGB = if ($NTDSDB -and (Test-Path $NTDSDB)) { [math]::Round((Get-Item $NTDSDB).Length / 1GB, 2) } else { $null }

        # Patches
        $LatestHotfix = $null
        $DaysSincePatch = $null
        try {
            $LatestHotfix = Get-HotFix | Where-Object InstalledOn | Sort-Object InstalledOn -Descending | Select-Object -First 1
            if ($LatestHotfix.InstalledOn) { $DaysSincePatch = [math]::Round(((Get-Date) - $LatestHotfix.InstalledOn).TotalDays, 0) }
        } catch { }
        
        $PendingReboot = ((Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") -or (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") -or (Test-Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations"))


        # Defender
        $DefenderEnabled = "Unable to check"
        $DefenderSigAge = "Unable to check"
        $DefenderVersion = $null
        $DefenderEngine = $null
        
        try {
            $Defender = Get-MpComputerStatus -ErrorAction Stop
            $DefenderEnabled = $Defender.RealTimeProtectionEnabled
            $DefenderSigAge = $Defender.AntivirusSignatureAge
            $DefenderVersion = $Defender.AntivirusSignatueVersion
            $DefenderEngine = $Defender.AMEngineVersion
        } catch { }

        # DC Info
        #$DCInfo = $null
        #try {
        #    $DCInfo = Get-ADDomainController -Identity $Computer -Server $TargetDomain -ErrorAction Stop
        #} catch { }
        try {
            $DCInfo = Get-ADDomainController -Identity $Computer -ErrorAction Stop
        }
        catch {
            $DCInfoError = $_.Exception.Message
        }
        # Network
        $NetworkAdapters = @()
        try {
            $NetworkAdapters = @(Get-NetAdapter | ForEach-Object {
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

        # DNS Configuration
        $DnsServers = @()
        try {
            $DnsServers = @(Get-DnsClientServerAddress -AddressFamily IPv4 | ForEach-Object {
                [PSCustomObject]@{
                    InterfaceAlias = $_.InterfaceAlias
                    DnsServers     = ($_.ServerAddresses -join ", ")
                }
            })
        } catch { }

        # FSMO Roles
        $OwnedFSMORoles = @()
        
        try {
            if ($DCInfo) {
                $OwnedFSMORoles = $DCInfo.OperationMasterRoles
            }
        } catch { }

        # Certificates
        $Certificates = @()
        
        try {
            $Certificates = @(Get-ChildItem Cert:\LocalMachine\My | ForEach-Object {
                [PSCustomObject]@{
                    Subject     = $_.Subject
                    Issuer      = $_.Issuer
                    Thumbprint  = $_.Thumbprint
                    NotAfter    = $_.NotAfter
                    DaysToExpiry = [math]::Round(($_.NotAfter - (Get-Date)).TotalDays,0)
                }
            })
        } catch { }
        
        # Recent Updates
        $RecentHotfixes = @()
        
        try {
            $RecentHotfixes = @(Get-HotFix |
                Sort-Object InstalledOn -Descending |
                Select-Object -First 10 |
                ForEach-Object {
                    [PSCustomObject]@{
                        HotFixID    = $_.HotFixID
                        InstalledOn = $_.InstalledOn
                        Description = $_.Description
                    }
                })
        }
        catch { }
        
        $Inventory = [PSCustomObject]@{
            ServerName              = $Computer
            FQDN                    = $TargetDC
            Site                    = if ($DCInfo) { $DCInfo.Site } else { "Unknown" }
            IPv4                    = if ($DCInfo) { $DCInfo.IPv4Address } else { "Unknown" }
            IsGlobalCatalog         = if ($DCInfo) { $DCInfo.IsGlobalCatalog } else { $null }
            IsReadOnlyDC            = if ($DCInfo) { $DCInfo.IsReadOnly } else { $null }
            OperatingSystem         = $OS.Caption
            OSVersion               = $OS.Version
            Build                   = $OS.BuildNumber
            Manufacturer            = $CS.Manufacturer
            Model                   = $CS.Model
            BIOS                    = $BIOS.SMBIOSBIOSVersion
            CPU                     = ($CPU.Name -join ", ")
            CPUCores                = ($CPU | Measure-Object NumberOfCores -Sum).Sum
            LogicalProcessors       = ($CPU | Measure-Object NumberOfLogicalProcessors -Sum).Sum
            CPULoadPercent          = [math]::Round($CpuLoad, 2)
            TotalMemoryGB           = [math]::Round($OS.TotalVisibleMemorySize / 1MB, 2)
            FreeMemoryGB            = [math]::Round($OS.FreePhysicalMemory / 1MB, 2)
            MemoryUsedPercent       = $MemoryUsedPercent
            LastBoot                = $OS.LastBootUpTime
            UptimeDays              = [math]::Round(((Get-Date) - $OS.LastBootUpTime).TotalDays, 2)
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
        }

        [PSCustomObject]@{
            Inventory = $Inventory
            Disks     = $Disks
            Services  = $Services
            Roles     = $Roles
            Apps      = $Apps
            Firewall  = $Firewall
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
