param(
    [string]$Domain,
    [int]$MaxSecurityEventsPerDC = 2000,
    [int]$EventReadTimeoutSeconds = 60,
    [int]$EventLookbackHours = 24
)
$ErrorActionPreference = "Stop"

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$Credential = $null
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {
    $DCs = @(Get-ADDomainController -Server $Domain -Filter * -Credential $Credential)
    
    $StartTime = (Get-Date).AddHours(-1 * $EventLookbackHours)
    $EventIds = @(4624,4625,4648,4740,4768,4769,4771,4776)

    $LogonRows = New-Object System.Collections.Generic.List[object]
    $Errors = New-Object System.Collections.Generic.List[string]

    $ScriptBlockStr = 'param($StartTime, $EventIds, $MaxEvents)' + "`n" + @'
    try {
        Get-WinEvent -LogName 'Security' -FilterHashtable @{ Id=$EventIds; StartTime=$StartTime } -MaxEvents $MaxEvents -ErrorAction Stop |
            ForEach-Object {
                [PSCustomObject]@{
                    Id = $_.Id
                    TimeCreated = $_.TimeCreated
                    Xml = $_.ToXml()
                }
            }
    } catch {
        [PSCustomObject]@{ IsError=$true; ErrorMessage=$_.Exception.Message }
    }
'@
    $ScriptBlock = [scriptblock]::Create($ScriptBlockStr)

    foreach ($DC in $DCs) {
        $InvokeErrors = $null

        $Job = Invoke-Command -ComputerName $DC.HostName -Credential $Credential -ErrorAction SilentlyContinue -ErrorVariable InvokeErrors -ArgumentList $StartTime, $EventIds, $MaxSecurityEventsPerDC -ScriptBlock $ScriptBlock -AsJob

        $Finished = Wait-Job -Job $Job -Timeout $EventReadTimeoutSeconds
        
        if (-not $Finished) {
            Stop-Job -Job $Job -Force | Out-Null
            Remove-Job -Job $Job -Force | Out-Null
            $Errors.Add("Timed out after $EventReadTimeoutSeconds seconds reading from $($DC.HostName).")
            continue
        }

        $RawEvents = @(Receive-Job -Job $Job)
        Remove-Job -Job $Job -Force | Out-Null

        if ($InvokeErrors) {
            $Errors.Add("Error reading from $($DC.HostName): $($InvokeErrors[0].Exception.Message)")
        }

        if ($RawEvents.Count -gt 0 -and $RawEvents[0].PSObject.Properties.Name -contains 'IsError') {
            $Errors.Add("Error reading from $($DC.HostName): $($RawEvents[0].ErrorMessage)")
            continue
        }

        foreach ($RawEvent in $RawEvents) {
            try {
                [xml]$Xml = $RawEvent.Xml
                
                # Inline parsing function
                $TargetUser = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'TargetUserName' }).'#text'
                $IpAddress = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'IpAddress' }).'#text'
                $LogonType = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'LogonType' }).'#text'
                $AuthPackage = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'AuthenticationPackageName' }).'#text'
                $Status = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'Status' }).'#text'
                $Workstation = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'WorkstationName' }).'#text'
                $TicketEncryptionType = ($Xml.Event.EventData.Data | Where-Object { $_.Name -eq 'TicketEncryptionType' }).'#text'
                
                if ([string]::IsNullOrWhiteSpace($TargetUser) -or $TargetUser -like "*$") { continue }

                $Findings = New-Object System.Collections.Generic.List[string]
                $AuthType = "Kerberos"
                
                if ($RawEvent.Id -eq 4624) {
                    if ($AuthPackage -eq "NTLM") { $AuthType = "NTLM" }
                    if ($AuthPackage -match "NTLM" -and $LogonType -in @(2,3,10)) { $Findings.Add("Clear-text / NTLM Logon") }
                } elseif ($RawEvent.Id -eq 4625) {
                    if ($AuthPackage -eq "NTLM") { $AuthType = "NTLM" }
                    $Findings.Add("Failed Logon: $Status")
                } elseif ($RawEvent.Id -eq 4768 -or $RawEvent.Id -eq 4769) {
                    if ($TicketEncryptionType -in @("0x1","0x2","0x3","0x4","0x17")) {
                        $Findings.Add("Weak Kerberos Encryption: $TicketEncryptionType")
                    }
                }

                $LogonRows.Add([PSCustomObject]@{
                    Time = $RawEvent.TimeCreated
                    EventId = $RawEvent.Id
                    User = $TargetUser
                    SourceDC = $DC.HostName
                    SourceIp = $IpAddress
                    Workstation = $Workstation
                    AuthType = $AuthType
                    Findings = ($Findings -join ", ")
                })
            } catch {}
        }
    }

    $Result = [PSCustomObject]@{
        Domain = $Domain
        LogonEvents = $LogonRows
        Errors = $Errors
    }
    
    Write-Output $Result
} catch {
    throw $_
}
