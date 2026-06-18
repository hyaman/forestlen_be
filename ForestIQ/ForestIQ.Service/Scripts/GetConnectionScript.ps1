            $ErrorActionPreference = 'Stop'
            
            $session = $null
            
            try {
            
                $sessionOption = New-PSSessionOption `
                    -SkipCACheck `
                    -SkipCNCheck
            
                $session = New-PSSession `
                    -ComputerName $env:PS_REMOTE_HOST `
                    -Authentication Kerberos `
                    -SessionOption $sessionOption
            
                $remoteResult = Invoke-Command `
                    -Session $session `
                    -ErrorAction Stop `
                    -ScriptBlock {
            
                        [pscustomobject]@{
                            ComputerName = $env:COMPUTERNAME
                        }
                    } |
                    Select-Object `
                        ComputerName
            
                $response = [pscustomobject]@{
                    Success = $true
                    Data = $remoteResult
                }
            
                [Console]::Out.WriteLine(
                    ($response | ConvertTo-Json -Depth 10 -Compress)
                )
            }
            catch {
            
                $response = [pscustomobject]@{
                    Success = $false
                    Error = $_.Exception.Message
                }
            
                [Console]::Out.WriteLine(
                    ($response | ConvertTo-Json -Compress)
                )
            
                exit 1
            }
            finally {
            
                if ($session) {
            
                    Remove-PSSession `
                        -Session $session `
                        -ErrorAction SilentlyContinue
                }
            }
            