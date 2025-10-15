Write-Host "Promoting main â†’ beta..." -ForegroundColor Cyan
git fetch origin
git checkout main
git pull origin main
if (-not (git show-ref --verify --quiet refs/heads/beta)) { git checkout -b beta } else { git checkout beta; git pull origin beta }
git merge main --no-edit
git push origin beta
$tag = "beta-promote-" + (Get-Date -Format "yyyy.MM.dd.HHmm")
git tag -a $tag -m "Promoted main â†’ beta"
git push origin $tag
Write-Host "Beta promotion complete."
