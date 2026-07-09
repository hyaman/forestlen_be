param(
    [string]$Domain,
    [int]$InactiveDays = 90,
    [int]$ComputerPasswordAgeWarningDays = 45,
    [int]$ComputerPasswordAgeCriticalDays = 90
)
$ErrorActionPreference = "Stop"

$username = if ($global:RemoteDomain) { "$global:RemoteDomain\$global:RemoteUsername" } else { $global:RemoteUsername }
$Credential = $null
if (-not [string]::IsNullOrWhiteSpace($username) -and -not [string]::IsNullOrWhiteSpace($global:RemotePassword)) {
    $securePassword = ConvertTo-SecureString $global:RemotePassword -AsPlainText -Force
    $Credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
}

try {
    $HasLegacyLapsExp = Test-SchemaAttribute $Domain "ms-Mcs-AdmPwdExpirationTime"
    $HasWinLapsExp = Test-SchemaAttribute $Domain "msLAPS-PasswordExpirationTime"

    $ComputerProps = @("Enabled","OperatingSystem","OperatingSystemVersion","DistinguishedName","Description","WhenCreated","WhenChanged","LastLogonDate","PasswordLastSet","ServicePrincipalName","UserAccountControl","AdminCount","msDS-SupportedEncryptionTypes","msDS-AllowedToDelegateTo","msDS-AllowedToActOnBehalfOfOtherIdentity")
    if ($HasLegacyLapsExp) { $ComputerProps += "ms-Mcs-AdmPwdExpirationTime" }
    if ($HasWinLapsExp) { $ComputerProps += "msLAPS-PasswordExpirationTime" }
    
    $Computers = @(Get-ADComputer -Server $Domain -Filter * -Properties $ComputerProps -Credential $Credential)

    $ComputerRisks = New-Object System.Collections.Generic.List[object]

    foreach ($Computer in $Computers) {
        $PasswordAge = Get-AgeDays $Computer.PasswordLastSet
        $SpnCount = 0
        if ($Computer.ServicePrincipalName) { $SpnCount = $Computer.ServicePrincipalName.Count }

        $LegacyExpVal = $null
        $WinExpVal = $null
        if ($HasLegacyLapsExp) { $LegacyExpVal = $Computer.'ms-Mcs-AdmPwdExpirationTime' }
        if ($HasWinLapsExp) { $WinExpVal = $Computer.'msLAPS-PasswordExpirationTime' }

        $Risks = New-Object System.Collections.Generic.List[string]
        if ($Computer.Enabled -eq $true -and $Computer.LastLogonDate -and ((Get-Date) - $Computer.LastLogonDate).TotalDays -ge $InactiveDays) {
            $Risks.Add("Stale")
        }
        if ($null -ne $PasswordAge) {
            if ($PasswordAge -ge $ComputerPasswordAgeCriticalDays) { $Risks.Add("OldPwdCritical") }
            elseif ($PasswordAge -ge $ComputerPasswordAgeWarningDays) { $Risks.Add("OldPwd") }
        }
        if (Get-UacFlag $Computer.UserAccountControl $UAC_TRUSTED_FOR_DELEGATION) {
            $Risks.Add("UnconstrainedDelegation")
        }
        if ($Computer.'msDS-AllowedToDelegateTo') {
            $Risks.Add("ConstrainedDelegation")
        }
        if ($Computer.AdminCount -eq 1) {
            $Risks.Add("AdminCount")
        }
        if ($HasLegacyLapsExp -or $HasWinLapsExp) {
            if (-not $LegacyExpVal -and -not $WinExpVal) {
                $Risks.Add("NoLAPS")
            }
        }

        $ComputerRisks.Add([PSCustomObject]@{
            Name = $Computer.Name
            Enabled = $Computer.Enabled
            OS = $Computer.OperatingSystem
            LastLogonDate = $Computer.LastLogonDate
            PasswordLastSet = $Computer.PasswordLastSet
            PasswordAgeDays = $PasswordAge
            SPNCount = $SpnCount
            AdminCount = $Computer.AdminCount
            LegacyLapsExpiration = $LegacyExpVal
            WindowsLapsExpiration = $WinExpVal
            Risks = ($Risks -join ", ")
        })
    }

    $Result = [PSCustomObject]@{
        Domain = $Domain
        Computers = $ComputerRisks
        LapsSchemaDetected = ($HasLegacyLapsExp -or $HasWinLapsExp)
    }
    
    Write-Output $Result
} catch {
    throw $_
}
