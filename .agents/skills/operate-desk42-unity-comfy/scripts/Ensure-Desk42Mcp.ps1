[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$Json,
    [ValidateRange(5, 300)]
    [int]$TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
}

function Get-LoopbackListener {
    param([int]$Port)

    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
    $addresses = @($listeners | ForEach-Object { $_.LocalAddress } | Sort-Object -Unique)
    $loopback = @($addresses | Where-Object { $_ -in @('127.0.0.1', '::1') })
    $exposed = @($addresses | Where-Object { $_ -notin @('127.0.0.1', '::1') })

    return [pscustomobject]@{
        IsListening = $loopback.Count -gt 0
        IsExposed = $exposed.Count -gt 0
        Addresses = $addresses
    }
}

function Test-ComfyBackend {
    try {
        $response = Invoke-RestMethod -Uri 'http://127.0.0.1:8188/system_stats' -TimeoutSec 3
        return $null -ne $response.system
    }
    catch {
        return $false
    }
}

function Test-UnityProjectProcess {
    param([string]$RepoRoot)

    $escaped = [regex]::Escape($RepoRoot)
    return $null -ne (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match $escaped } |
        Select-Object -First 1)
}

function Start-UnityProject {
    param([string]$RepoRoot)

    $versionFile = Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt'
    $versionLine = Get-Content $versionFile | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
    if (-not $versionLine) {
        throw "Cannot read the Unity version from $versionFile"
    }

    $version = ($versionLine -split ':', 2)[1].Trim()
    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    $unityExe = Join-Path $programFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path $unityExe)) {
        throw "Unity $version was not found at $unityExe"
    }

    Start-Process -FilePath $unityExe -ArgumentList @('-projectPath', ('"{0}"' -f $RepoRoot)) | Out-Null
}

function Start-ComfyBackend {
    $userProfile = [Environment]::GetFolderPath('UserProfile')
    $comfyRoot = if ($env:DESK42_COMFY_ROOT) { $env:DESK42_COMFY_ROOT } else { Join-Path $userProfile 'ComfyUI-Installs\ComfyUI' }
    $sharedRoot = if ($env:DESK42_COMFY_SHARED) { $env:DESK42_COMFY_SHARED } else { Join-Path $userProfile 'ComfyUI-Shared' }
    $pythonExe = Join-Path $comfyRoot 'comfy-env\Scripts\python.exe'
    $mainPy = Join-Path $comfyRoot 'ComfyUI\main.py'
    $extraPaths = Join-Path $comfyRoot 'ComfyUI\extra_model_paths.yaml'
    $outputDir = Join-Path $sharedRoot 'output'

    foreach ($requiredPath in @($pythonExe, $mainPy, $extraPaths)) {
        if (-not (Test-Path $requiredPath)) {
            throw "Required ComfyUI path is missing: $requiredPath"
        }
    }

    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }

    $arguments = @(
        ('"{0}"' -f $mainPy),
        '--listen', '127.0.0.1',
        '--port', '8188',
        '--extra-model-paths-config', ('"{0}"' -f $extraPaths),
        '--output-directory', ('"{0}"' -f $outputDir)
    ) -join ' '

    Start-Process -FilePath $pythonExe -ArgumentList $arguments -WorkingDirectory $comfyRoot -WindowStyle Hidden | Out-Null
}

function Get-Status {
    $unityListener = Get-LoopbackListener -Port 8080
    $comfyListener = Get-LoopbackListener -Port 8188
    return [pscustomobject]@{
        UnityMcpReady = $unityListener.IsListening -and -not $unityListener.IsExposed
        UnityAddresses = @($unityListener.Addresses)
        ComfyUiReady = $comfyListener.IsListening -and -not $comfyListener.IsExposed -and (Test-ComfyBackend)
        ComfyUiAddresses = @($comfyListener.Addresses)
        LoopbackOnly = -not $unityListener.IsExposed -and -not $comfyListener.IsExposed
    }
}

$repoRoot = Get-RepoRoot
$status = Get-Status

if (-not $CheckOnly) {
    if (-not $status.ComfyUiReady) {
        Write-Verbose 'Starting the ComfyUI backend.'
        Start-ComfyBackend
    }

    if (-not $status.UnityMcpReady -and -not (Test-UnityProjectProcess -RepoRoot $repoRoot)) {
        Write-Verbose 'Opening the Desk 42 Unity project.'
        Start-UnityProject -RepoRoot $repoRoot
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $status = Get-Status
        if ($status.UnityMcpReady -and $status.ComfyUiReady -and $status.LoopbackOnly) {
            break
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
}

if ($Json) {
    $status | ConvertTo-Json -Depth 3 -Compress
}
else {
    $status | Format-List
}

if (-not $status.LoopbackOnly) {
    throw 'An MCP/backend port is exposed beyond loopback. Stop it and restore localhost-only binding before use.'
}

if (-not $status.ComfyUiReady) {
    throw 'ComfyUI is not healthy on http://127.0.0.1:8188. Check the ComfyUI process and logs.'
}

if (-not $status.UnityMcpReady) {
    throw 'Unity MCP is not ready on http://127.0.0.1:8080/mcp. In the open editor use Window > MCP for Unity, then retry.'
}
