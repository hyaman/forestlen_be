$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($username, $securePassword)

if ($TargetDC -eq 'All' -or [string]::IsNullOrEmpty($TargetDC)) {
    $DCs = Get-ADDomainController -Credential $cred -Filter * | Select-Object -ExpandProperty HostName
} else {
    $DCs = @($TargetDC)
}

$results = @()

foreach ($DCName in $DCs) {
    $RemoteData = Invoke-Command -ComputerName $DCName -Credential $cred -ArgumentList $TargetDomain, $DCName -ScriptBlock {
        param($TargetDomain, $TargetDC)
        $Computer = $env:COMPUTERNAME

        $NTDSEvents = @()
        try {
            $NTDSEvents = @(Get-WinEvent -FilterHashtable @{ LogName="Directory Service"; Level=2; StartTime=(Get-Date).AddDays(-7) } -ErrorAction Stop)
        } catch { }

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
    } -ErrorAction SilentlyContinue

    if ($RemoteData) {
        $results += $RemoteData
    }
}

return $results