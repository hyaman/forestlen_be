# ============================================================
# ForestIQ - Replication Discovery Engine
# Forest + Child Domains
# ============================================================

Import-Module ActiveDirectory -ErrorAction Stop

Clear-Host

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " ForestIQ - Replication Discovery Engine" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# ============================================================
# Credentials
# ============================================================

$ForestIQCredential = Get-Credential -Message "Enter ForestIQ Service Account"

# ============================================================
# Forest Discovery
# ============================================================

try
{
    $Forest = Get-ADForest
}
catch
{
    Write-Host "Unable to discover forest." -ForegroundColor Red
    return
}

$ReplicationReport = @()

# ============================================================
# Discover Domains
# ============================================================

foreach ($Domain in $Forest.Domains)
{
    Write-Host ""
    Write-Host "Discovering Domain: $Domain" -ForegroundColor Yellow

    try
    {
        $DCs = Get-ADDomainController `
            -Server $Domain `
            -Filter *
    }
    catch
    {
        Write-Host "Failed discovering DCs in $Domain" -ForegroundColor Red
        continue
    }

    foreach ($DC in $DCs)
    {
        Write-Host "Checking replication on $($DC.HostName)" -ForegroundColor Cyan

        try
        {
            $Partners = Get-ADReplicationPartnerMetadata `
                -Target $DC.HostName `
                -Scope Server `
                -ErrorAction Stop |
                Where-Object { $_.Partner }

            foreach ($Partner in $Partners)
            {
                $LatencyMinutes = [math]::Round(
                    ((Get-Date) - $Partner.LastReplicationSuccess).TotalMinutes,
                    0
                )

                if ($Partner.ConsecutiveReplicationFailures -gt 0)
                {
                    $Health = "FAIL"
                }
                elseif ($LatencyMinutes -le 15)
                {
                    $Health = "PASS"
                }
                elseif ($LatencyMinutes -le 60)
                {
                    $Health = "WARNING"
                }
                elseif ($LatencyMinutes -le 240)
                {
                    $Health = "HIGH WARNING"
                }
                else
                {
                    $Health = "CRITICAL"
                }

                # ----------------------------------------------------
                # Extract Source DC Name
                # ----------------------------------------------------

                $SourceDCName = "Unknown"

                if ($Partner.Partner -match "CN=NTDS Settings,CN=([^,]+)")
                {
                    $SourceDCName = $Matches[1]
                }
                else
                {
                    $SourceDCName = $Partner.Partner
                }

                # ----------------------------------------------------
                # Store Result
                # ----------------------------------------------------

                $ReplicationReport += [PSCustomObject]@{

                    Domain                    = $Domain

                    SourceDCName              = $SourceDCName

                    SourceDCFullDN            = $Partner.Partner

                    DestinationDC             = $Partner.Server

                    PartnerType               = $Partner.PartnerType

                    Partition                 = $Partner.Partition

                    LastReplicationSuccess    = $Partner.LastReplicationSuccess

                    LastReplicationAttempt    = $Partner.LastReplicationAttempt

                    LatencyMinutes            = $LatencyMinutes

                    ConsecutiveFailures       = $Partner.ConsecutiveReplicationFailures

                    LastReplicationResult     = $Partner.LastReplicationResult

                    Health                    = $Health
                }
            }
        }
        catch
        {
            Write-Host "Failed collecting replication from $($DC.HostName)" -ForegroundColor Red
            Write-Host $_.Exception.Message -ForegroundColor Red
        }
    }
}

# ============================================================
# Summary Output
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " ForestIQ Replication Summary" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

$ReplicationReport =
$ReplicationReport |
Sort-Object Domain,DestinationDC,Partition

foreach ($Item in $ReplicationReport)
{
    switch ($Item.Health)
    {
        "PASS"         { $Colour = "Green" }
        "WARNING"      { $Colour = "Yellow" }
        "HIGH WARNING" { $Colour = "DarkYellow" }
        "CRITICAL"     { $Colour = "Red" }
        "FAIL"         { $Colour = "Red" }
        default        { $Colour = "White" }
    }

    Write-Host "Domain            : $($Item.Domain)" -ForegroundColor $Colour
    Write-Host "Source DC Name    : $($Item.SourceDCName)" -ForegroundColor $Colour
    Write-Host "Source DC Full DN : $($Item.SourceDCFullDN)" -ForegroundColor DarkGray
    Write-Host "Destination DC    : $($Item.DestinationDC)" -ForegroundColor $Colour
    Write-Host "Partition         : $($Item.Partition)" -ForegroundColor $Colour
    Write-Host "Partner Type      : $($Item.PartnerType)" -ForegroundColor $Colour
    Write-Host "Last Success      : $($Item.LastReplicationSuccess)" -ForegroundColor $Colour
    Write-Host "Latency           : $($Item.LatencyMinutes) Minutes" -ForegroundColor $Colour
    Write-Host "Failures          : $($Item.ConsecutiveFailures)" -ForegroundColor $Colour
    Write-Host "Last Result       : $($Item.LastReplicationResult)" -ForegroundColor $Colour
    Write-Host "Health            : $($Item.Health)" -ForegroundColor $Colour
    Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
}

# ============================================================
# Statistics
# ============================================================

$PassLinks        = ($ReplicationReport | Where-Object {$_.Health -eq "PASS"}).Count
$WarningLinks     = ($ReplicationReport | Where-Object {$_.Health -eq "WARNING"}).Count
$HighWarningLinks = ($ReplicationReport | Where-Object {$_.Health -eq "HIGH WARNING"}).Count
$CriticalLinks    = ($ReplicationReport | Where-Object {$_.Health -eq "CRITICAL"}).Count
$FailLinks        = ($ReplicationReport | Where-Object {$_.Health -eq "FAIL"}).Count

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " ForestIQ Replication Statistics" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host "PASS         : $PassLinks" -ForegroundColor Green
Write-Host "WARNING      : $WarningLinks" -ForegroundColor Yellow
Write-Host "HIGH WARNING : $HighWarningLinks" -ForegroundColor DarkYellow
Write-Host "CRITICAL     : $CriticalLinks" -ForegroundColor Red
Write-Host "FAIL         : $FailLinks" -ForegroundColor Red

Write-Host ""
Write-Host "Legend:" -ForegroundColor Cyan
Write-Host "PASS         = 0-15 Minutes" -ForegroundColor Green
Write-Host "WARNING      = 15-60 Minutes" -ForegroundColor Yellow
Write-Host "HIGH WARNING = 1-4 Hours" -ForegroundColor DarkYellow
Write-Host "CRITICAL     = More than 4 Hours" -ForegroundColor Red
Write-Host "FAIL         = Replication Failures Detected" -ForegroundColor Red