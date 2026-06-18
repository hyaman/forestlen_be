Import-Module ActiveDirectory -ErrorAction Stop

$report = [pscustomobject][ordered]@{
    GeneratedAt         = Get-Date
    PortConnectivity    = @()
    DcDiagSummary       = $null
    Errors              = @()
    Timings             = [System.Collections.Generic.List[object]]::new()
}

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

$dcs = @()
try { 
    $forest = Get-ADForest -Credential $cred
    foreach ($domain in $forest.Domains) {
        if ($global:FilterDomain -and ($domain -notmatch $global:FilterDomain)) { continue }
        try {
            $domainDCs = @(Get-ADDomainController -Filter * -Server $domain -Credential $cred -ErrorAction Stop | Select-Object HostName, Site)
            if ($global:FilterSite) { $domainDCs = @($domainDCs | Where-Object { $_.Site -match $global:FilterSite }) }
            if ($null -ne $domainDCs -and $domainDCs.Count -gt 0) { $dcs += $domainDCs }
        } catch { }
    }
}
catch { $report.Errors += [pscustomobject]@{ Section = 'DomainControllers'; Error = $_.Exception.Message }; return $report }

$t0 = Get-Date
try
{
    $ADPorts = [ordered]@{ DNS = 53; Kerberos = 88; RPC = 135; LDAP = 389; SMB = 445; KerberosPassword = 464; LDAPS = 636; GlobalCatalog = 3268; GlobalCatalogSSL = 3269; ADWS = 9389; WinRM = 5985 }
    $TimeoutMs = 1000
    $tasks = [System.Collections.Generic.List[hashtable]]::new()

    foreach ($dc in $dcs)
    {
        foreach ($port in $ADPorts.GetEnumerator())
        {
            $tcp = [System.Net.Sockets.TcpClient]::new()
            $task = $tcp.ConnectAsync($dc.HostName, $port.Value)
            $tasks.Add(@{ Tcp = $tcp; Task = $task; DomainController = $dc.HostName; SiteName = $dc.Site; Service = $port.Key; Port = $port.Value })
        }
    }

    [System.Threading.Tasks.Task[]]$allTasks = $tasks | ForEach-Object { $_.Task }
    $portResults = [System.Collections.Generic.List[object]]::new()

    try
    {
        $deadline = [System.Threading.Tasks.Task]::WhenAll($allTasks)
        [void]$deadline.Wait($TimeoutMs)

        foreach ($t in $tasks)
        {
            $open = $t.Task.Status -eq [System.Threading.Tasks.TaskStatus]::RanToCompletion -and $t.Tcp.Connected
            $portResults.Add([pscustomobject]@{ DomainController = $t.DomainController; SiteName = $t.SiteName; Service = $t.Service; Port = $t.Port; Open = $open })
        }
    }
    finally { foreach ($t in $tasks) { try { $t.Tcp.Close() } catch {} } }

    $report.PortConnectivity = $portResults | Sort-Object DomainController, Port
}
catch { $report.Errors += [pscustomobject]@{ Section = 'PortConnectivity'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'PortConnectivity'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

function Get-DcDiagIssue
{
    param([string]$Output)
    switch -Regex ($Output)
    {
        '(?i)Directory Binding Error 5|Access is denied' { return @{ Status = 'Error'; Category = 'Permissions'; Details = 'Access denied while querying the Domain Controller.' } }
        '(?i)Record registrations cannot be found' { return @{ Status = 'Warning'; Category = 'DNS'; Details = 'Domain Controller DNS records are missing or not registered.' } }
        '(?i)forwarders.*invalid' { return @{ Status = 'Warning'; Category = 'DNS'; Details = 'DNS forwarders are invalid or unreachable.' } }
        '(?i)failed test DNS' { return @{ Status = 'Warning'; Category = 'DNS'; Details = 'DNS health test failed.' } }
        '(?i)failed test Replications' { return @{ Status = 'Critical'; Category = 'Replication'; Details = 'Active Directory replication failures detected.' } }
        '(?i)RPC server is unavailable' { return @{ Status = 'Error'; Category = 'Connectivity'; Details = 'RPC communication with the Domain Controller failed.' } }
        '(?i)Unable to contact the server' { return @{ Status = 'Error'; Category = 'Connectivity'; Details = 'Domain Controller is unreachable.' } }
        '(?i)SysVolCheck' { return @{ Status = 'Critical'; Category = 'SYSVOL'; Details = 'SYSVOL replication is unhealthy.' } }
        '(?i)NetLogons' { return @{ Status = 'Critical'; Category = 'NetLogon'; Details = 'NetLogon share is unavailable.' } }
        default { return @{ Status = 'Issues Found'; Category = 'Other'; Details = $Output } }
    }
}

$t0 = Get-Date
try
{
    $perDcDiag = [System.Collections.Generic.List[object]]::new()
    $dcdiagUser = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
    $dcdiagPass = $global:RemotePassword

            $runningProcs = @()
            foreach ($dc in $dcs)
            {
                $dcdiagArgs = @("/s:$($dc.HostName)", "/test:Advertising", "/test:Services", "/test:DNS", "/DnsBasic", "/DnsRecordRegistration", "/q")
                if ($dcdiagUser) {
                    $dcdiagArgs += "/u:$dcdiagUser"
                    $dcdiagArgs += "/p:$dcdiagPass"
                }

                $guid = [guid]::NewGuid().ToString()
                $outPath = "$env:TEMP\dcdiag_$guid.log"
                $errPath = "$env:TEMP\dcdiag_err_$guid.log"

                $p = Start-Process -FilePath "dcdiag.exe" -ArgumentList $dcdiagArgs -NoNewWindow -PassThru -RedirectStandardOutput $outPath -RedirectStandardError $errPath
                $runningProcs += [pscustomobject]@{
                    DC = $dc
                    Process = $p
                    OutPath = $outPath
                    ErrPath = $errPath
                }
            }

            foreach ($item in $runningProcs)
            {
                $dc = $item.DC
                $p = $item.Process
                
                try
                {
                    $exited = $p.WaitForExit(120000)
                    
                    if (-not $exited) {
                        $p.Kill()
                    }

                    $stdout = if (Test-Path $item.OutPath) { Get-Content $item.OutPath -Raw } else { "" }
                    $stderr = if (Test-Path $item.ErrPath) { Get-Content $item.ErrPath -Raw } else { "" }
                    
                    Remove-Item $item.OutPath -ErrorAction SilentlyContinue
                    Remove-Item $item.ErrPath -ErrorAction SilentlyContinue

                    $outputString = "$stdout`n$stderr".Trim()

                    if (-not $exited) {
                        $perDcDiag.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Status = "Error"; Category = "Timeout"; Details = "Job timed out after 120 seconds. Review RawOutput for partial logs."; RawOutput = $outputString })
                    }
                    elseif ([string]::IsNullOrWhiteSpace($outputString) -or $outputString -match "passed test") {
                        $perDcDiag.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Status = "Healthy"; Category = "None"; Details = "No issues detected."; RawOutput = $outputString })
                    } else {
                        $issue = Get-DcDiagIssue $outputString
                        $perDcDiag.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Status = $issue.Status; Category = $issue.Category; Details = $issue.Details; RawOutput = $outputString })
                    }
                }
                catch { $perDcDiag.Add([pscustomobject]@{ DomainController = $dc.HostName; SiteName = $dc.Site; Status = "Error"; Details = $_.Exception.Message; RawOutput = $null }) }
            }
    $report.DcDiagSummary = $perDcDiag
}
catch { $report.Errors += [pscustomobject]@{ Section = 'DcDiagSummary'; Error = $_.Exception.Message } }
$report.Timings.Add([pscustomobject]@{ Section = 'DcDiagSummary'; ElapsedSeconds = [math]::Round(((Get-Date) - $t0).TotalSeconds, 2) })

$report
