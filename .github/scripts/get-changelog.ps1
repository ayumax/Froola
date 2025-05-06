param(
    [string]$PreviousTag,
    [string]$OutputFile
)

Write-Host "Previous tag: $PreviousTag"

# Create a file for PR titles
New-Item -Path $OutputFile -ItemType File -Force | Out-Null

# If there is no previous tag, get all merged PRs to main
if ([string]::IsNullOrEmpty($PreviousTag)) {
    Write-Host "No previous tag found. Getting all merged PRs to main."
    gh pr list --state merged --base main --json title,number --jq '.[] | "- #\(.number) \(.title)"' > $OutputFile
} else {
    Write-Host "Getting PRs between $PreviousTag and HEAD"
    
    # Get all commits between previous tag and HEAD
    $commitRange = "$PreviousTag..HEAD"
    Write-Host "Commit range: $commitRange"
    
    # Get all PR numbers that are referenced in commit messages in the range
    $prNumbers = @()
    $commits = git log $commitRange --oneline
    
    foreach ($commit in $commits) {
        # Look for PR references like "Merge pull request #123" or "(#123)"
        if ($commit -match "(pull request|PR) #(\d+)" -or $commit -match "\(#(\d+)\)") {
            $prNumber = if ($matches[2]) { $matches[2] } else { $matches[1] }
            if ($prNumber -and -not ($prNumbers -contains $prNumber)) {
                $prNumbers += $prNumber
            }
        }
    }
    
    Write-Host "Found PRs in commit messages: $prNumbers"
    
    # Get details for each PR and add to changelog
    foreach ($prNumber in $prNumbers) {
        $prInfo = gh pr view $prNumber --json number,title
        if ($prInfo) {
            $pr = $prInfo | ConvertFrom-Json
            "- #$($pr.number) $($pr.title)" | Out-File -FilePath $OutputFile -Append
        }
    }
    
    # If no PRs found using commit messages, try another approach
    if ($prNumbers.Count -eq 0) {
        Write-Host "No PRs found in commit messages, trying another approach"
        
        # Get all merged PRs
        $allPRs = gh pr list --state merged --base main --json number,title,mergeCommit --jq '.[]' | ConvertFrom-Json
        
        # Get all commit hashes in the range
        $commitHashes = git log --pretty=format:"%H" $commitRange
        
        # Filter PRs by merge commit hash
        foreach ($pr in $allPRs) {
            if ($pr.mergeCommit -and $commitHashes -contains $pr.mergeCommit.oid) {
                "- #$($pr.number) $($pr.title)" | Out-File -FilePath $OutputFile -Append
            }
        }
    }
}

# Read PR titles if file exists and has content
if ((Test-Path $OutputFile) -and (Get-Item $OutputFile).Length -gt 0) {
    $changelog = Get-Content $OutputFile | Out-String
    Write-Host "Changelog content:"
    Write-Host $changelog
    return $changelog
} else {
    Write-Warning "No PR titles found. Setting changelog to empty."
    return "No changes found in this release."
}