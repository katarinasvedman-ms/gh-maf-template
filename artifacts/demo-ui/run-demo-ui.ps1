param(
    [int]$Port = 5507,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactsRoot = Split-Path -Parent $root
Set-Location -Path $root

$prefix = "http://localhost:$Port/"
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($prefix)
$listener.Start()

Write-Host "Run Artifact Viewer is serving from: $root"
Write-Host "URL: $prefix"
Write-Host "Press Ctrl+C to stop."

if (-not $NoOpen) {
    Start-Process $prefix | Out-Null
}

$mimeTypes = @{
    '.html' = 'text/html; charset=utf-8'
    '.css'  = 'text/css; charset=utf-8'
    '.js'   = 'application/javascript; charset=utf-8'
    '.json' = 'application/json; charset=utf-8'
    '.png'  = 'image/png'
    '.jpg'  = 'image/jpeg'
    '.jpeg' = 'image/jpeg'
    '.svg'  = 'image/svg+xml'
    '.ico'  = 'image/x-icon'
}

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $requestPath = $context.Request.Url.AbsolutePath.TrimStart('/')
        if ([string]::IsNullOrWhiteSpace($requestPath)) {
            $requestPath = 'index.html'
        }

        # Provide stable same-origin aliases for artifact files so the UI can auto-load
        # without relying on parent-directory traversal, which many static servers block.
        if ($requestPath -eq 'evaluation-report.json') {
            $safePath = Join-Path $artifactsRoot 'evaluation-report.json'
        }
        elseif ($requestPath -eq 'evaluation-library-report.json') {
            $safePath = Join-Path $artifactsRoot 'evaluation-library-report.json'
        }
        else {
            $safePath = [System.IO.Path]::GetFullPath((Join-Path $root $requestPath))
            if (-not $safePath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
                $context.Response.StatusCode = 400
                $context.Response.Close()
                continue
            }
        }

        if (-not (Test-Path -Path $safePath -PathType Leaf)) {
            $context.Response.StatusCode = 404
            $bytes = [System.Text.Encoding]::UTF8.GetBytes("Not Found")
            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $context.Response.Close()
            continue
        }

        $ext = [System.IO.Path]::GetExtension($safePath).ToLowerInvariant()
        $contentType = if ($mimeTypes.ContainsKey($ext)) { $mimeTypes[$ext] } else { 'application/octet-stream' }

        $fileBytes = [System.IO.File]::ReadAllBytes($safePath)
        $context.Response.StatusCode = 200
        $context.Response.ContentType = $contentType
        $context.Response.ContentLength64 = $fileBytes.LongLength
        $context.Response.OutputStream.Write($fileBytes, 0, $fileBytes.Length)
        $context.Response.Close()
    }
}
finally {
    if ($listener.IsListening) {
        $listener.Stop()
    }
    $listener.Close()
}
