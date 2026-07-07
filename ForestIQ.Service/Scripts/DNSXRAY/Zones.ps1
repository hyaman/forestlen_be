

$ZoneInventory = @()

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {
    # If DnsServer is 'All', we need to fetch all DNS servers first
    $TargetServers = @()
    if ($DnsServer -eq 'All') {
        $ADParams = @{}
        if ($Credential) {
            $ADParams.Credential = $Credential
            if ($global:RemoteDomain) {
                $ADParams.Server = $global:RemoteDomain
            }
        }
        $Forest = Get-ADForest @ADParams -ErrorAction Stop
        $Domains = $Forest.Domains
        if ($DomainFilter -ne 'All') {
            $Domains = $Domains | Where-Object { $_ -like "*$DomainFilter*" }
        }

        foreach ($Domain in $Domains) {
            $DomainADParams = $ADParams.Clone()
            $DomainADParams.Server = $Domain
            $DCs = Get-ADDomainController -Filter * @DomainADParams -ErrorAction Stop

            if ($SiteFilter -ne 'All') {
                $DCs = $DCs | Where-Object { $_.Site -eq $SiteFilter }
            }
            if ($TargetDC -ne 'All') {
                $DCs = $DCs | Where-Object { $_.HostName -eq $TargetDC -or $_.Name -eq $TargetDC }
            }

            foreach ($DC in $DCs) {
                if ($TargetServers -notcontains $DC.HostName) {
                    $TargetServers += $DC.HostName
                }
            }
        }
    } else {
        $TargetServers += $DnsServer
    }

    $TargetServers = $TargetServers | Select-Object -Unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if ($TargetServers.Count -gt 0) {
        $InvokeErrors = $null
        $remoteResults = Invoke-Command -ComputerName $TargetServers -Credential $Credential -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock {
            $Server = $env:COMPUTERNAME
            $LocalInventory = @()
            try {
                $Zones = Get-DnsServerZone -ErrorAction Stop
                foreach ($Zone in $Zones) {
                    $LocalInventory += [PSCustomObject]@{
                        DnsServer           = $Server
                        ZoneName            = $Zone.ZoneName
                        ZoneType            = $Zone.ZoneType
                        IsDsIntegrated      = $Zone.IsDsIntegrated
                        ReplicationScope    = $Zone.ReplicationScope
                        IsReverseLookupZone = $Zone.IsReverseLookupZone
                        DynamicUpdate       = $Zone.DynamicUpdate
                        SecureSecondaries   = $Zone.SecureSecondaries
                        IsPaused            = $Zone.IsPaused
                        IsShutdown          = $Zone.IsShutdown
                    }
                }
            } catch {
                # Silently skip servers we can't query
            }
            return $LocalInventory
        }

        if ($remoteResults) {
            foreach ($res in $remoteResults) {
                $ZoneInventory += [PSCustomObject]@{
                    DnsServer           = $res.DnsServer
                    ZoneName            = $res.ZoneName
                    ZoneType            = $res.ZoneType
                    IsDsIntegrated      = $res.IsDsIntegrated
                    ReplicationScope    = $res.ReplicationScope
                    IsReverseLookupZone = $res.IsReverseLookupZone
                    DynamicUpdate       = $res.DynamicUpdate
                    SecureSecondaries   = $res.SecureSecondaries
                    IsPaused            = $res.IsPaused
                    IsShutdown          = $res.IsShutdown
                }
            }
        }
    }
    
    if ($ZoneInventory.Count -eq 0) {
        @() | Write-Output
    } else {
        $ZoneInventory | Write-Output
    }
}
catch {
    throw $_
}
