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

$results = @()

foreach ($DCName in $DCs) {
    try {
        $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName -ErrorAction Stop -ScriptBlock {
            param($TargetDomain, $TargetDC)
            $Computer = $env:COMPUTERNAME

            $NTDSEvents = @()
            try {
                $NTDSEvents = @(Get-WinEvent -FilterHashtable @{ LogName="Directory Service"; Level=2; StartTime=(Get-Date).AddDays(-7) } -ErrorAction Stop)
            } catch { 
            
                 if ($_.Exception.Message -like "*No events were found*") {
                     $NTDSEvents = @()
                 }
                 else {
                     $Health = "ERROR"
                     $NTDSEvents = @(
                         [PSCustomObject]@{
                             TimeCreated  = Get-Date
                             Id           = -1
                             ProviderName = "ForestIQ"
                             Message      = "Failed to query Directory Service event log. Exception: $($_.Exception.Message)"
                         }
                     )
                 }
            }

            $NTDSEventSummary = @(
                $NTDSEvents | Select-Object -First 10 | ForEach-Object {
                    [PSCustomObject]@{
                        TimeCreated = $_.TimeCreated
                        EventID     = $_.Id
                        Provider    = $_.ProviderName
                        Message     = ($_.Message -replace "`r|`n", " ")
                    }
                }
            )

            [PSCustomObject]@{
                ServerName       = $Computer
                FQDN             = $TargetDC
                Health           = if ($NTDSEvents.Count -gt 0) { "WARNING" } else { "OK" }
                ErrorsLast7Days  = $NTDSEvents.Count
                RecentErrors     = $NTDSEventSummary
            }
        }
    } catch {
        $RemoteData = [PSCustomObject]@{
            ServerName       = $DCName
            FQDN             = $DCName
            Health           = "ERROR"
            ErrorsLast7Days  = 0
            RecentErrors     = @(
                [PSCustomObject]@{
                    TimeCreated = Get-Date
                    EventID     = -1
                    Provider    = "WinRM"
                    Message     = "Failed to connect to DC via WinRM: $($_.Exception.Message)"
                }
            )
        }
    }

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results