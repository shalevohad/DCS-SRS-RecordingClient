Write-Host "Promoting beta â†’ release..." -ForegroundColor Cyan
git fetch origin
git checkout beta
git pull origin beta
if (-not (git show-ref --verify --quiet refs/heads/release)) { git checkout -b release } else { git checkout release; git pull origin release }
git merge beta --no-edit
git push origin release
$ver = "v" + (Get-Date -Format "yyyy.MM.dd.HHmm")
git tag -a $ver -m "Official AeroDebrief Release $ver"
git push origin $ver
Write-Host "Release promotion complete."
