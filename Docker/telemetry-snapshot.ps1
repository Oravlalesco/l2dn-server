param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')]
    [string]$Label = "npc-test"
)

$ErrorActionPreference = "Stop"
$endpoint = "http://localhost:9464/metrics"
$snapshotDirectory = Join-Path $PSScriptRoot "telemetry\snapshots"
New-Item -ItemType Directory -Path $snapshotDirectory -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$snapshotPath = Join-Path $snapshotDirectory "$timestamp-$Label.prom"
$response = Invoke-WebRequest -UseBasicParsing -Uri $endpoint
[System.IO.File]::WriteAllText($snapshotPath, $response.Content, [System.Text.UTF8Encoding]::new($false))

$metricPattern = '^(l2dn_(players_online|npc_(combat|loaded|thinking|reaction_latency|scheduler_(queue_delay|queue_depth|reactive_mode|active_workers|singleflight_collision|execution_failure)|wakeup_(total|coalesced|dropped)|think_pending_followup)))'
$relevant = $response.Content -split "`n" | Where-Object { $_ -match $metricPattern }

Write-Host "NPC telemetry snapshot: $snapshotPath"
Write-Host "Prometheus endpoint: $endpoint"
Write-Host "Relevant samples: $($relevant.Count)"
$relevant
