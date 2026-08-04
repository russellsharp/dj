# delete_old_artifacts.ps1
param (
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [int]$Hours
)

$ErrorActionPreference = "Stop"

# Verify GitHub CLI installation
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI (gh) is not installed."
}

# Calculate cutoff time in UTC
$CutoffTime = (Get-Date).AddHours(-$Hours).ToUniversalTime()
Write-Host "Fetching artifacts for $Repo..."
Write-Host "Cutoff threshold (UTC): $CutoffTime"

# Fetch all artifacts via GitHub CLI pagination
try {
    $ArtifactsRaw = gh api "/repos/$Repo/actions/artifacts" --paginate | ConvertFrom-Json
}
catch {
    Write-Error "Failed to fetch artifacts. Verify repo path or authentication status."
}

# Flatten pagination response payload items
$Artifacts = $ArtifactsRaw | ForEach-Object { $_.artifacts }

if (-not $Artifacts) {
    Write-Host "No artifacts found."
    exit
}

$DeletedCount = 0

foreach ($Artifact in $Artifacts) {
    $CreatedAt = [DateTime]::Parse($Artifact.created_at).ToUniversalTime()
    
    if ($CreatedAt -lt $CutoffTime) {
        Write-Host "Deleting artifact '$($Artifact.name)' (ID: $($Artifact.id), Created: $($Artifact.created_at))..."
        
        try {
            gh api -X DELETE "/repos/$Repo/actions/artifacts/$($Artifact.id)" --silent
            $DeletedCount++
        }
        catch {
            Write-Warning "Failed to delete artifact ID $($Artifact.id)."
        }
    }
}

Write-Host "Cleanup completed. Total artifacts deleted: $DeletedCount"
