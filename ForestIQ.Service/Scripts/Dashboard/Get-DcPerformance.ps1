$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = @()
    try {
        $Forest = Get-ADForest -Credential $cred
        $Domains = $Forest.Domains
        foreach ($DomainName in $Domains) {
            $DomainDCs = Get-ADDomainController -Server $DomainName -Credential $cred -Filter *
            if ($DomainDCs) {
                $DCs += $DomainDCs | Select-Object -ExpandProperty HostName
            }
        }
    } catch {
        $DomainDCs = Get-ADDomainController -Credential $cred -Filter *
        $DCs = $DomainDCs | Select-Object -ExpandProperty HostName
    }
} else {
    $DCs = @($TargetDC)
}

$results = @()

foreach ($DCName in $DCs) {
    $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName -ScriptBlock {
        param($TargetDomain, $TargetDC)
        $Computer = $TargetDC

        try {
            # Let the initial WinRM session startup spike subside slightly
            Start-Sleep -Milliseconds 500

            # 1. System Info & Uptime
            $OS = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
            $UptimeDays = [math]::Round(((Get-Date) - $OS.LastBootUpTime).TotalDays, 2)
            $MemoryUsage = [math]::Round((($OS.TotalVisibleMemorySize - $OS.FreePhysicalMemory) / $OS.TotalVisibleMemorySize) * 100, 2)

            # 2. CPU
            $CpuLoad = 0
            try {
                # Get-Counter natively samples over 1 second, completely ignoring momentary spikes
                $Counter = Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 1 -ErrorAction Stop
                $CpuLoad = [math]::Round($Counter.CounterSamples[0].CookedValue, 2)
            } catch {
                # Fallback for non-English OS where '\Processor' performance counter name differs
                Start-Sleep -Seconds 2
                $CPU = Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue
                if ($CPU) {
                    $CpuLoad = [math]::Round(($CPU | Measure-Object LoadPercentage -Average).Average, 2)
                }
            }

            # 3. Disks
            $Disks = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" -ErrorAction SilentlyContinue
            $LocalDisks = @()
            $DiskCFree = 0
            $DiskCSizeGB = 0
            $DiskCFreeGB = 0

            if ($Disks) {
                foreach ($disk in $Disks) {
                    $SizeGB = [math]::Round($disk.Size / 1GB, 2)
                    $FreeGB = [math]::Round($disk.FreeSpace / 1GB, 2)
                    $LocalDisks += [PSCustomObject]@{
                        DriveLetter = $disk.DeviceID
                        SizeGB      = $SizeGB
                        FreeGB      = $FreeGB
                    }
                    if ($disk.DeviceID -eq 'C:') {
                        $DiskCFree = [math]::Round(($disk.FreeSpace / $disk.Size) * 100, 2)
                        $DiskCSizeGB = $SizeGB
                        $DiskCFreeGB = $FreeGB
                    }
                }
            }

            # 4. Top 10 Processes by CPU/Memory
            $cpuInfo = Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue
            $cores = [int]($cpuInfo | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
            if (-not $cores -or $cores -eq 0) { $cores = 1 }
            $CpuModel = if ($cpuInfo) { ($cpuInfo | Select-Object -First 1).Name } else { "Unknown" }

            $ProcessCounters = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -ErrorAction SilentlyContinue
            if ($ProcessCounters) {
                $Processes = $ProcessCounters | Where-Object { $_.Name -notmatch "_Total|Idle" } | Sort-Object PercentProcessorTime -Descending | Select-Object -First 10 | ForEach-Object {
                    [PSCustomObject]@{
                        ProcessName = ($_.Name -replace '#\d+$', '') + ".exe"
                        CpuPercent  = [math]::Round(($_.PercentProcessorTime / $cores), 4)
                        MemoryMB    = [math]::Round(($_.WorkingSet / 1MB), 1)
                        Handles     = $_.HandleCount
                        Threads     = $_.ThreadCount
                    }
                }
            } else {
                # Fallback if performance counters are corrupted/unavailable
                $Processes = Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 10 | ForEach-Object {
                    [PSCustomObject]@{
                        ProcessName = $_.ProcessName + ".exe"
                        CpuPercent  = 0
                        MemoryMB    = [math]::Round($_.WorkingSet64 / 1MB, 1)
                        Handles     = $_.HandleCount
                        Threads     = $_.Threads.Count
                    }
                }
            }

            # 5. Network I/O (in Percentage)
            $NetCounters = Get-CimInstance Win32_PerfFormattedData_Tcpip_NetworkInterface -ErrorAction SilentlyContinue
            $NetworkIo = 0
            if ($NetCounters) {
                $TotalBytes = 0
                $TotalBandwidth = 0
                foreach ($adapter in $NetCounters) {
                    if ($adapter.CurrentBandwidth -gt 0 -and $adapter.Name -notmatch "Loopback|isatap|Teredo") {
                        $TotalBytes += $adapter.BytesTotalPersec
                        $TotalBandwidth += ($adapter.CurrentBandwidth / 8) # Convert Bits/sec to Bytes/sec
                    }
                }
                if ($TotalBandwidth -gt 0) {
                    $NetworkPercent = ($TotalBytes / $TotalBandwidth) * 100
                    if ($NetworkPercent -gt 100) { $NetworkPercent = 100 }
                    $NetworkIo = [math]::Round($NetworkPercent, 2)
                }
            }

            [PSCustomObject]@{
                ServerName          = $Computer
                CpuLoad             = $CpuLoad
                MemoryUsage         = $MemoryUsage
                DiskCFree           = $DiskCFree
                DiskCSizeGB         = $DiskCSizeGB
                DiskCFreeGB         = $DiskCFreeGB
                LocalDisks          = $LocalDisks
                TotalProcesses      = $OS.NumberOfProcesses
                VirtualMemorySizeKB = $OS.TotalVirtualMemorySize
                FreeVirtualMemoryKB = $OS.FreeVirtualMemory
                CpuModel            = $CpuModel
                LogicalCores        = $cores
                UptimeDays          = $UptimeDays
                LastBoot            = $OS.LastBootUpTime
                OsVersion           = "$($OS.Caption) (Build $($OS.BuildNumber))"
                TopProcesses        = $Processes
                NetworkIo           = $NetworkIo
            }
        }
        catch {
            [PSCustomObject]@{
                ServerName          = $Computer
                CpuLoad             = 0
                MemoryUsage         = 0
                DiskCFree           = 0
                DiskCSizeGB         = 0
                DiskCFreeGB         = 0
                LocalDisks          = @()
                TotalProcesses      = 0
                VirtualMemorySizeKB = 0
                FreeVirtualMemoryKB = 0
                CpuModel            = "Unknown"
                LogicalCores        = 0
                UptimeDays          = 0
                LastBoot            = $null
                OsVersion           = "Unknown"
                TopProcesses        = @()
                NetworkIo           = 0
                Error               = $_.Exception.Message
            }
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
