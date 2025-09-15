param(
    [Parameter(Mandatory = $false)]
    [string]$BuildSourceBranch,
    
    [Parameter(Mandatory = $false)]
    [string]$BuildReason
)

# Function to parse version from Directory.Build.props
function Get-CurrentVersion {
    $propsFile = "Directory.Build.props"
    if (-not (Test-Path $propsFile)) {
        throw "Directory.Build.props not found"
    }
    
    [xml]$xml = Get-Content $propsFile
    $versionPrefix = $xml.Project.PropertyGroup.VersionPrefix
    
    if (-not $versionPrefix) {
        throw "VersionPrefix not found in Directory.Build.props"
    }
    
    return $versionPrefix
}

# Function to update version in Directory.Build.props
function Update-VersionInProps {
    param([string]$NewVersion)
    
    $propsFile = "Directory.Build.props"
    [xml]$xml = Get-Content $propsFile
    
    $xml.Project.PropertyGroup.VersionPrefix = $NewVersion
    $xml.Save((Resolve-Path $propsFile))
    
    Write-Host "Updated version to $NewVersion in Directory.Build.props"
}

# Function to increment patch version
function Get-IncrementedVersion {
    param([string]$CurrentVersion)
    
    if ($CurrentVersion -match '^(\d+)\.(\d+)\.(\d+)$') {
        $major = [int]$matches[1]
        $minor = [int]$matches[2]
        $patch = [int]$matches[3] + 1
        
        return "$major.$minor.$patch"
    }
    else {
        throw "Invalid version format: $CurrentVersion"
    }
}


# Main execution
try {
    Write-Host "Starting version management script..."
    Write-Host "Build Source Branch: $BuildSourceBranch"
    Write-Host "Build Reason: $BuildReason"
    
    # Get current version
    $currentVersion = Get-CurrentVersion
    Write-Host "Current version: $currentVersion"
    
    # Check if this should trigger a version increment
    # In Azure DevOps, we want to increment when:
    # 1. We're building the master branch
    # 2. The source branch was develop (indicating a merge from develop to master)
    $shouldIncrement = $false
    
    if ($BuildSourceBranch -eq "refs/heads/master" -or $BuildSourceBranch -eq "refs/heads/main") {
        # Check if this is a merge from develop by looking at the commit message
        $lastCommitMessage = git log -1 --pretty=format:"%s"
        Write-Host "Checking commit message: $lastCommitMessage"
        
        if ($lastCommitMessage -match "Merge.*develop.*master|Merge.*develop.*main|Merge pull request.*develop") {
            Write-Host "Detected develop to master merge from commit message"
            $shouldIncrement = $true
        }
        else {
            # Alternative check using git merge-base to see if develop was recently merged
            $developCommit = git rev-parse origin/develop 2>$null
            $masterCommit = git rev-parse HEAD
            
            if ($developCommit) {
                $mergeBase = git merge-base $developCommit $masterCommit 2>$null
                if ($mergeBase -eq $developCommit) {
                    Write-Host "Detected that develop branch was merged (develop is ancestor of current commit)"
                    $shouldIncrement = $true
                }
            }
        }
    }
    
    if ($shouldIncrement) {
        Write-Host "Detected develop to master merge - incrementing version"
        
        # Increment version
        $newVersion = Get-IncrementedVersion -CurrentVersion $currentVersion
        Write-Host "New version: $newVersion"
        
        # Update Directory.Build.props
        Update-VersionInProps -NewVersion $newVersion
        
        # Create git tag
        $tagName = "v$newVersion"
        Write-Host "Creating tag: $tagName"
        
        git config user.email "build@z-bit.com"
        git config user.name "Azure DevOps Build"
        
        git add Directory.Build.props
        git commit -m "Bump version to $newVersion"
        git tag -a $tagName -m "Release version $newVersion"
        
        # Push tag to origin
        git push origin $tagName
        Write-Host "Pushed tag $tagName to origin"
        
        # Switch to develop and merge the version update
        Write-Host "Switching to develop branch to merge version update"
        git checkout develop
        git merge master --no-ff -m "Merge version update from master"
        git push origin develop
        Write-Host "Merged version update to develop branch"
        
        # Switch back to master
        git checkout master
        
        # Set output variables for Azure DevOps
        Write-Host "##vso[task.setvariable variable=NewVersion;isOutput=true]$newVersion"
        Write-Host "##vso[task.setvariable variable=VersionIncremented;isOutput=true]true"
    }
    else {
        Write-Host "No version increment needed"
        Write-Host "##vso[task.setvariable variable=NewVersion;isOutput=true]$currentVersion"
        Write-Host "##vso[task.setvariable variable=VersionIncremented;isOutput=true]false"
    }
    
    # Always output current version for build purposes
    Write-Host "##vso[task.setvariable variable=BuildVersion;isOutput=true]$currentVersion"
    
    Write-Host "Version management script completed successfully"
}
catch {
    Write-Error "Error in version management script: $_"
    exit 1
}