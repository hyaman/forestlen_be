$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = Get-ADDomainController -Credential $cred -Filter * | Select-Object -ExpandProperty HostName
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
                }
            })
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
                }
            })
        } catch { }

        $Kerberos4624Count = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -eq "Kerberos" }).Count
        $NTLM4624Count     = @($SuccessfulLogons | Where-Object { $_.AuthenticationPackage -match "NTLM" }).Count
        $KerberosTGTCount  = $KerberosTGTRequests.Count
        $NTLMValidationCount = $NTLMValidations.Count

        $AuthProtocolSummary = @(
            [PSCustomObject]@{ Protocol = "Kerberos - 4624"; Count = $Kerberos4624Count }
            [PSCustomObject]@{ Protocol = "NTLM - 4624"; Count = $NTLM4624Count }
            [PSCustomObject]@{ Protocol = "Kerberos - TGT 4768"; Count = $KerberosTGTCount }
            [PSCustomObject]@{ Protocol = "NTLM - Validations 4776"; Count = $NTLMValidationCount }
        )

        $TopAuthenticatedUsers = @($SuccessfulLogons | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count } })
        $RecentNTLMUsers = @($NTLMValidations | Where-Object { $_.UserName -and -not (Test-IsComputerAccount $_.UserName) } | Group-Object UserName | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { [PSCustomObject]@{ UserName = $_.Name; Count = $_.Count } })

        [PSCustomObject]@{
            ServerName            = $Computer
            FQDN                  = $TargetDC
            AuthProtocolSummary   = $AuthProtocolSummary
            TopAuthenticatedUsers = $TopAuthenticatedUsers
            RecentNTLMUsers       = $RecentNTLMUsers
        }
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results
