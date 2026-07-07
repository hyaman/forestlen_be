

Import-Module DnsServer -ErrorAction Stop

$SOAComparison = @()

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {
    # Get all DNS servers hosting this zone (or just the selected one)
    $DnsServers = @()
    if ([string]::IsNullOrWhiteSpace($DnsServer) -or $DnsServer -eq 'All') {
        $ADParams = @{}
        if ($Credential) {
            $ADParams.Credential = $Credential
            if ($global:RemoteDomain) {
                $ADParams.Server = $global:RemoteDomain
            }
        }
        $Forest = Get-ADForest @ADParams -ErrorAction Stop
        foreach ($Domain in $Forest.Domains) {
            $DomainADParams = $ADParams.Clone()
            $DomainADParams.Server = $Domain
            $DCs = Get-ADDomainController -Filter * @DomainADParams -ErrorAction Stop
            foreach ($DC in $DCs) {
                if ($DnsServers -notcontains $DC.HostName) {
                    $DnsServers += $DC.HostName
                }
            }
        }
    } else {
        $DnsServers += $DnsServer
    }

    $DnsServers = $DnsServers | Select-Object -Unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $SOAInventory = @()

    if ($DnsServers.Count -gt 0) {
        $InvokeErrors = $null
        $remoteResults = Invoke-Command -ComputerName $DnsServers -Credential $Credential -ArgumentList $ZoneName -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ScriptBlock {
            param($ZoneName)
            $Server = $env:COMPUTERNAME
            $LocalInventory = @()
            try {
                if ([string]::IsNullOrWhiteSpace($ZoneName) -or $ZoneName -eq 'All') {
                    $Zones = Get-DnsServerZone -ErrorAction Stop
                } else {
                    $Zones = @(Get-DnsServerZone -ZoneName $ZoneName -ErrorAction Stop)
                }
                foreach ($Zone in $Zones) {
                    $CurrentZoneName = $Zone.ZoneName
                    $Records = Get-DnsServerResourceRecord -ZoneName $CurrentZoneName -RRType SOA -ErrorAction SilentlyContinue
                    foreach ($Record in $Records) {
                        $LocalInventory += [PSCustomObject]@{
                            DnsServer        = $Server
                            ZoneName         = $CurrentZoneName
                            PrimaryServer    = $Record.RecordData.PrimaryServer
                            SerialNumber     = $Record.RecordData.SerialNumber
                            RefreshInterval  = $Record.RecordData.RefreshInterval
                            RetryDelay       = $Record.RecordData.RetryDelay
                            ExpireLimit      = $Record.RecordData.ExpireLimit
                            MinimumTTL       = $Record.RecordData.MinimumTimeToLive.TotalSeconds
                        }
                    }
                }
            } catch {
                # Server doesn't host this zone or is offline
            }
            return $LocalInventory
        }

        if ($remoteResults) {
            foreach ($res in $remoteResults) {
                $SOAInventory += [PSCustomObject]@{
                    DnsServer        = $res.DnsServer
                    ZoneName         = $res.ZoneName
                    PrimaryServer    = $res.PrimaryServer
                    SerialNumber     = $res.SerialNumber
                    RefreshInterval  = $res.RefreshInterval
                    RetryDelay       = $res.RetryDelay
                    ExpireLimit      = $res.ExpireLimit
                    MinimumTTL       = $res.MinimumTTL
                }
            }
        }
    }

    $GroupedByZone = $SOAInventory | Group-Object ZoneName
    foreach ($Group in $GroupedByZone) {
        $Serials = $Group.Group | Select-Object -ExpandProperty SerialNumber -Unique
        $Status = "Healthy"
        if ($Serials.Count -gt 1) {
            $Status = "Warning"
        }

        foreach ($Item in $Group.Group) {
            $Item | Add-Member -MemberType NoteProperty -Name "Status" -Value $Status
            $SOAComparison += $Item
        }
    }

    if ($SOAComparison.Count -eq 0) {
        @() | Write-Output
    } else {
        $SOAComparison | Write-Output
    }
}
catch {
    throw $_
}
