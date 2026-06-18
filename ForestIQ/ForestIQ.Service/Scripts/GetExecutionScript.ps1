                $ErrorActionPreference = 'Continue'
                
                $session = $null
                
                try {
                
                    $sessionOption = New-PSSessionOption `
                        -SkipCACheck `
                        -SkipCNCheck
                
                    $session = New-PSSession `
                        -ComputerName $env:PS_REMOTE_HOST `
                        -Authentication Kerberos `
                        -SessionOption $sessionOption
                
                    $command  = $env:PS_REMOTE_COMMAND
                    $domain   = $env:PS_REMOTE_DOMAIN
                    $username = $env:PS_REMOTE_USERNAME
                    $password = $env:PS_REMOTE_PASSWORD
                    $filterDomain = $env:PS_FILTER_DOMAIN
                    $filterSite   = $env:PS_FILTER_SITE
                
                    $result = Invoke-Command `
                        -Session $session `
                        -ArgumentList $command, $domain, $username, $password, $filterDomain, $filterSite `
                        -ScriptBlock {
                
                            param($cmd, $dom, $usr, $pwd, $fDom, $fSite)
                            
                            $global:RemoteDomain   = $dom
                            $global:RemoteUsername = $usr
                            $global:RemotePassword = $pwd
                            $global:FilterDomain   = $fDom
                            $global:FilterSite     = $fSite
                
                            Invoke-Expression $cmd
                        }
                
                if ($null -ne $result)
                {
                    $result = $result |
                        Select-Object * `
                            -ExcludeProperty RunspaceId,
                                             PSComputerName,
                                             PSShowComputerName
                }
                    $response = [pscustomobject]@{
                        Success = $true
                        Data = $result
                    }
                
                    [Console]::Out.WriteLine(
                        ($response | ConvertTo-Json -Depth 20 -Compress)
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
                