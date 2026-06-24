$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = @()
    try {
        $Forest = Get-ADForest
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

$results = @()

foreach ($DCName in $DCs) {
    $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName -ScriptBlock {
        param($TargetDomain, $TargetDC)
        $Computer = $env:COMPUTERNAME

        $LiveSessions = @()
        try {
            $QUserRaw = quser 2>$null
            if ($QUserRaw) {
                $LiveSessions = @(
                    $QUserRaw | Select-Object -Skip 1 | ForEach-Object {
                        $Line = ($_ -replace "^\s+", "") -replace "\s{2,}", ","
                        $Parts = $Line.Split(",")
                        [PSCustomObject]@{
                            ServerName  = $Computer
                            UserName    = if ($Parts.Count -ge 1) { $Parts[0] } else { "Unknown" }
                            SessionName = if ($Parts.Count -ge 2) { $Parts[1] } else { "Unknown" }
                            ID          = if ($Parts.Count -ge 3) { $Parts[2] } else { "Unknown" }
                            State       = if ($Parts.Count -ge 4) { $Parts[3] } else { "Unknown" }
                            IdleTime    = if ($Parts.Count -ge 5) { $Parts[4] } else { "Unknown" }
                            LogonTime   = if ($Parts.Count -ge 6) { ($Parts[5..($Parts.Count - 1)] -join " ") } else { "Unknown" }
                            IsRemote    = if (($Parts -join " ") -match "rdp|ica") { $true } else { $false }
                        }
                    }
                )
            }
        } catch { }

        [PSCustomObject]@{
            ServerName   = $Computer
            FQDN         = $TargetDC
            LiveSessions = $LiveSessions
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
