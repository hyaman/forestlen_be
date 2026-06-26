$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = @()
    try {
        $Forest = Get-ADForest
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
        $Computer = $env:COMPUTERNAME

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

            # 3. Disk (C:)
            $DiskC = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'" -ErrorAction Stop
            $DiskCFree = 0
            if ($DiskC) {
                $DiskCFree = [math]::Round(($DiskC.FreeSpace / $DiskC.Size) * 100, 2)
            }

            # 4. Top 10 Processes by CPU/Memory
            $Processes = Get-Process | Sort-Object CPU -Descending | Select-Object -First 10 | ForEach-Object {
                [PSCustomObject]@{
                    ProcessName = $_.ProcessName + ".exe"
                    CPUPercent  = if ($_.CPU) { [math]::Round($_.CPU, 2) } else { 0 }
                    MemoryMB    = [math]::Round($_.WorkingSet64 / 1MB, 1)
                    Handles     = $_.HandleCount
                    Threads     = $_.Threads.Count
                }
            }

            # 5. Network I/O (in KB/s)
            $NetCounters = Get-CimInstance Win32_PerfFormattedData_Tcpip_NetworkInterface -ErrorAction SilentlyContinue
            $NetworkIo = 0
            if ($NetCounters) {
                $TotalBytes = ($NetCounters | Measure-Object BytesTotalPersec -Sum).Sum
                $NetworkIo = [math]::Round(($TotalBytes / 1KB), 2)
            }

            [PSCustomObject]@{
                ServerName   = $Computer
                CpuLoad      = $CpuLoad
                MemoryUsage  = $MemoryUsage
                DiskCFree    = $DiskCFree
                UptimeDays   = $UptimeDays
                LastBoot     = $OS.LastBootUpTime
                OsVersion    = "$($OS.Caption) (Build $($OS.BuildNumber))"
                TopProcesses = $Processes
                NetworkIo    = $NetworkIo
            }
        }
        catch {
            [PSCustomObject]@{
                ServerName   = $Computer
                CpuLoad      = 0
                MemoryUsage  = 0
                DiskCFree    = 0
                UptimeDays   = 0
                TopProcesses = @()
                NetworkIo    = 0
                Error        = $_.Exception.Message
            }
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
