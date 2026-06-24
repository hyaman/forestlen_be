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

if (-not $AuthLookBackHours) { $AuthLookBackHours = 24 }

$results = @()

foreach ($DCName in $DCs) {
    $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName, $AuthLookBackHours -ScriptBlock {
        param($TargetDomain, $TargetDC, $AuthLookBackHours)
        $Computer = $env:COMPUTERNAME
        $AuthStartTime = (Get-Date).AddHours(-$AuthLookBackHours)

        function Get-EventDataValue {
            param([System.Diagnostics.Eventing.Reader.EventRecord]$Event, [string]$Name)
            try { return ([xml]$Event.ToXml()).Event.EventData.Data | Where-Object { $_.Name -eq $Name } | Select-Object -ExpandProperty "#text" } catch { return $null }
        }

        function Test-IsComputerAccount {
            param([string]$Name)
            if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
            return $Name.EndsWith('$')
        }

        $SuccessfulLogons = @()
        try {
            $SuccessfulLogons = @(Get-WinEvent -FilterHashtable @{ LogName="Security"; Id=4624; StartTime=$AuthStartTime } -ErrorAction Stop | ForEach-Object {
                $TargetUserName = Get-EventDataValue -Event $_ -Name "TargetUserName"
                if ($TargetUserName -and -not (Test-IsComputerAccount $TargetUserName) -and $TargetUserName -notin @("ANONYMOUS LOGON", "SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE")) {
                    [PSCustomObject]@{
                        TimeCreated           = $_.TimeCreated
                        UserName              = "$(Get-EventDataValue -Event $_ -Name "TargetDomainName")\$TargetUserName"
                        LogonType             = Get-EventDataValue -Event $_ -Name "LogonType"
                        SourceIP              = Get-EventDataValue -Event $_ -Name "IpAddress"
                        SourceWorkstation     = Get-EventDataValue -Event $_ -Name "WorkstationName"
                        AuthenticationPackage = Get-EventDataValue -Event $_ -Name "AuthenticationPackageName"
                        LogonProcess          = Get-EventDataValue -Event $_ -Name "LogonProcessName"
                    }
                }
            })
        } catch { }

        $KerberosTGTRequests = @()
        try {
            $KerberosTGTRequests = @(Get-WinEvent -FilterHashtable @{ LogName="Security"; Id=4768; StartTime=$AuthStartTime } -ErrorAction Stop | ForEach-Object {
                [PSCustomObject]@{
                    TimeCreated   = $_.TimeCreated
                    UserName      = Get-EventDataValue -Event $_ -Name "TargetUserName"
                    SourceIP      = Get-EventDataValue -Event $_ -Name "IpAddress"
                    Status        = Get-EventDataValue -Event $_ -Name "Status"
                    TicketOptions = Get-EventDataValue -Event $_ -Name "TicketOptions"
                    AuthProtocol  = "Kerberos"
                    EventID       = 4768
                    EventType     = "Kerberos TGT Request"
                }
            })
        } catch { }

        $KerberosServiceTickets = @()
        try {
            $KerbServiceEvents = Get-WinEvent -FilterHashtable @{ LogName="Security"; Id=4769; StartTime=$AuthStartTime } -ErrorAction Stop
            $KerberosServiceTickets = @(
                $KerbServiceEvents | ForEach-Object {
                    [PSCustomObject]@{
                        TimeCreated   = $_.TimeCreated
                        UserName      = Get-EventDataValue -Event $_ -Name "TargetUserName"
                        ServiceName   = Get-EventDataValue -Event $_ -Name "ServiceName"
                        SourceIP      = Get-EventDataValue -Event $_ -Name "IpAddress"
                        Status        = Get-EventDataValue -Event $_ -Name "Status"
                        TicketOptions = Get-EventDataValue -Event $_ -Name "TicketOptions"
                        AuthProtocol  = "Kerberos"
                        EventID       = 4769
                        EventType     = "Kerberos Service Ticket"
                    }
                }
            )
        } catch { }

        $NTLMValidations = @()
        try {
            $NTLMValidations = @(Get-WinEvent -FilterHashtable @{ LogName="Security"; Id=4776; StartTime=$AuthStartTime } -ErrorAction Stop | ForEach-Object {
                [PSCustomObject]@{
                    TimeCreated       = $_.TimeCreated
                    UserName          = Get-EventDataValue -Event $_ -Name "TargetUserName"
                    SourceWorkstation = Get-EventDataValue -Event $_ -Name "Workstation"
                    PackageName       = Get-EventDataValue -Event $_ -Name "PackageName"
                    Status            = Get-EventDataValue -Event $_ -Name "Status"
                    AuthProtocol      = "NTLM"
                    EventID           = 4776
                    EventType         = "NTLM Credential Validation"
                }
            })
        } catch { }

        $Kerberos4624Count = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -eq "Kerberos" }).Count
        $NTLM4624Count     = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -match "NTLM" }).Count
        $Other4624Count    = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -and $_.AuthenticationPackage -notmatch "Kerberos|NTLM" }).Count
        $KerberosTGTCount  = $KerberosTGTRequests.Count
        $KerberosServiceCount = $KerberosServiceTickets.Count
        $NTLMValidationCount = $NTLMValidations.Count

        $AuthProtocolSummary = @(
            [PSCustomObject]@{ Protocol = "Kerberos - 4624 Logon Package"; Count = $Kerberos4624Count; Notes = "Successful logons using Kerberos package" }
            [PSCustomObject]@{ Protocol = "NTLM - 4624 Logon Package"; Count = $NTLM4624Count; Notes = "Successful logons using NTLM package" }
            [PSCustomObject]@{ Protocol = "Other - 4624 Logon Package"; Count = $Other4624Count; Notes = "Other successful logon packages" }
            [PSCustomObject]@{ Protocol = "Kerberos - TGT Requests 4768"; Count = $KerberosTGTCount; Notes = "Kerberos ticket-granting-ticket requests" }
            [PSCustomObject]@{ Protocol = "Kerberos - Service Tickets 4769"; Count = $KerberosServiceCount; Notes = "Kerberos service ticket requests" }
            [PSCustomObject]@{ Protocol = "NTLM - Credential Validation 4776"; Count = $NTLMValidationCount; Notes = "NTLM validations handled by this DC" }
        )

        $TopAuthenticatedUsers = @($SuccessfulLogons | Where-Object { $_.UserName -and $_.UserName -notmatch "Unable to check" } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count } })
        
        $TopSourceComputers = @($SuccessfulLogons | Where-Object { $_.SourceWorkstation -and $_.SourceWorkstation -notin @("-", "Unknown", "") } | Group-Object SourceWorkstation | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ SourceWorkstation = $_.Name; Count = $_.Count } })
        
        $RecentNTLMUsers = @($NTLMValidations | Where-Object { $_.UserName -and -not (Test-IsComputerAccount $_.UserName) } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count; Status = "WARNING - NTLM observed" } })

        $RecentKerberosUsers = @($KerberosTGTRequests | Where-Object { $_.UserName -and -not (Test-IsComputerAccount $_.UserName) } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count; Status = "OK - Kerberos observed" } })

        [PSCustomObject]@{
            ServerName            = $Computer
            FQDN                  = $TargetDC
            AuthProtocolSummary   = $AuthProtocolSummary
            TopAuthenticatedUsers = $TopAuthenticatedUsers
            TopSourceComputers    = $TopSourceComputers
            RecentKerberosUsers   = $RecentKerberosUsers
            RecentNTLMUsers       = $RecentNTLMUsers
            SuccessfulLogons      = $SuccessfulLogons
            KerberosTGTRequests   = $KerberosTGTRequests
            KerberosServiceTickets= $KerberosServiceTickets
            NTLMValidations       = $NTLMValidations
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
