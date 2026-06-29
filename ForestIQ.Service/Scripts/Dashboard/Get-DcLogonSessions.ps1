$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = @()
    try {
        $Forest = Get-ADForest -Credential $cred
        if ($ForestFilter -ne 'All' -and -not [string]::IsNullOrEmpty($ForestFilter) -and $Forest.Name -ne $ForestFilter) {
            $Domains = @()
        }
        else {
            $Domains = if ($DomainFilter -ne 'All' -and -not [string]::IsNullOrEmpty($DomainFilter)) {
                @($DomainFilter)
            }
            else {
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
    }
    catch {
        $DomainDCs = Get-ADDomainController -Credential $cred -Filter *
        if ($SiteFilter -ne 'All' -and -not [string]::IsNullOrEmpty($SiteFilter)) {
            $DomainDCs = $DomainDCs | Where-Object Site -eq $SiteFilter
        }
        $DCs = $DomainDCs | Select-Object -ExpandProperty HostName
    }
}
else {
    $DCs = @($TargetDC)
}

$results = @()

foreach ($DCName in $DCs) {
    try {
        $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName -ErrorAction Stop -ScriptBlock {
            param($TargetDomain, $TargetDC)
            $Computer = $env:COMPUTERNAME

            function Convert-IdleTime {
                param([string]$IdleTime)

                switch -Regex ($IdleTime) {
                    '^none$' { return 'Active' }
                    '^(\d+)\+(\d+):(\d+)$' { return "$($Matches[1]) days $($Matches[2]) hours $($Matches[3]) minutes" }
                    '^(\d+):(\d+)$' { return "$($Matches[1]) hours $($Matches[2]) minutes" }
                    '^\d+$' { return "$IdleTime minutes" }
                    default { return $IdleTime }
                }
            }

            $LiveSessions = @()
            try {
                $QUserRaw = quser 2>$null

                if ($QUserRaw) {
                    $RawOutput = $QUserRaw -join "`n"
                    $LiveSessions = @(
                        $QUserRaw | Select-Object -Skip 1 | ForEach-Object {
                            $Line = $_.TrimStart()

                            # SESSIONNAME present
                            if ($Line -match '^(\S+)\s+(\S+)\s+(\d+)\s+(Active|Disc)\s+(\S+)\s+(.+)$') {
                                [PSCustomObject]@{
                                    ServerName  = $Computer
                                    UserName    = $Matches[1]
                                    SessionName = $Matches[2]
                                    ID          = $Matches[3]
                                    State       = $Matches[4]
                                    IdleTime    = Convert-IdleTime $Matches[5]
                                    LogonTime   = $Matches[6]
                                    IsRemote    = $Matches[2] -match 'rdp|ica'
                                }
                            }
                            # SESSIONNAME missing (common for disconnected sessions)
                            elseif ($Line -match '^(\S+)\s+(\d+)\s+(Active|Disc)\s+(\S+)\s+(.+)$') {
                                [PSCustomObject]@{
                                    ServerName  = $Computer
                                    UserName    = $Matches[1]
                                    SessionName = ''
                                    ID          = $Matches[2]
                                    State       = $Matches[3]
                                    IdleTime    = Convert-IdleTime $Matches[4]
                                    LogonTime   = $Matches[5]
                                    IsRemote    = $false
                                }
                            }
                        }
                    )
                }
            }
            catch { 
                $ErrorMsg = $_.Exception.Message
            }

            [PSCustomObject]@{
                ServerName   = $Computer
                FQDN         = $TargetDC
                LiveSessions = $LiveSessions
                Error        = $ErrorMsg
                RawOutput    = $RawOutput
            }
        }
    } catch {
        $RemoteData = [PSCustomObject]@{
            ServerName   = $DCName
            FQDN         = $DCName
            LiveSessions = @()
            Error        = "Failed to connect to DC via WinRM: $($_.Exception.Message)"
            RawOutput    = $null
        }
    }

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
