param(
    [string]$Repository = 'mrcheng/leftright',
    [string]$BuildDirectory = 'Builds/WebGL'
)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($Repository -notmatch '^[\w.-]+/[\w.-]+$') { throw 'Invalid repository name.' }
$build = (Resolve-Path (Join-Path $workspace $BuildDirectory)).Path
if (!(Test-Path -LiteralPath (Join-Path $build 'index.html'))) { throw 'Build the browser game first.' }
if (& git -C $workspace status --porcelain) { throw 'Commit source changes before publishing so the build has a reviewable source revision.' }
$source = & git -C $workspace rev-parse HEAD
$stage = Join-Path $workspace ('Temp/pages-' + [Guid]::NewGuid().ToString('N'))
# A fresh staging directory avoids deleting or checking out files in the Unity workspace.
New-Item -ItemType Directory -Path $stage -Force | Out-Null
function Invoke-PagesGit([string[]]$Arguments) {
    $priorPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & git -C $stage @Arguments; $gitExit = $LASTEXITCODE }
    finally { $ErrorActionPreference = $priorPreference }
    if ($gitExit -ne 0) { throw "Git failed: $($Arguments[0])" }
}
Invoke-PagesGit -Arguments @('init','-b','gh-pages')
Invoke-PagesGit -Arguments @('remote','add','origin',"https://github.com/$Repository.git")
$existing = & git -C $stage ls-remote --heads origin refs/heads/gh-pages
if ($LASTEXITCODE -ne 0) { throw 'Could not read publishing branch.' }
if ($existing) { Invoke-PagesGit -Arguments @('fetch','--depth=1','origin','gh-pages'); Invoke-PagesGit -Arguments @('checkout','-B','gh-pages','FETCH_HEAD') }
foreach ($item in Get-ChildItem -LiteralPath $build -Force) {
    if ($item.Name -like '*BackUpThisFolder*') { continue }
    Copy-Item -LiteralPath $item.FullName -Destination $stage -Recurse -Force
}
if (Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.Length -gt 100MB }) { throw 'A build file exceeds the GitHub Git file limit.' }
Set-Content -LiteralPath (Join-Path $stage '.gitattributes') -Value '* -text'
@{source=$source;builtUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'build-info.json')
$author = & git -C $workspace config user.name
$email = & git -C $workspace config user.email
if (!$author) { $author = ($Repository -split '/')[0] }
if (!$email) { $email = "$author@users.noreply.github.com" }
Invoke-PagesGit -Arguments @('config','user.name',$author); Invoke-PagesGit -Arguments @('config','user.email',$email)
Invoke-PagesGit -Arguments @('add','--all'); Invoke-PagesGit -Arguments @('commit','-m',"Publish browser game $($source.Substring(0,8))")
Invoke-PagesGit -Arguments @('push','origin','gh-pages')
$config = Join-Path $workspace 'Temp/pages-config.json'
@{build_type='legacy';source=@{branch='gh-pages';path='/'}} | ConvertTo-Json | Set-Content -LiteralPath $config
$hasPages = & gh api "repos/$Repository" --jq '.has_pages'
$method = if ($hasPages -eq 'true') { 'PUT' } else { 'POST' }
& gh api --method $method "repos/$Repository/pages" --input $config --silent
if ($LASTEXITCODE -ne 0 -and $method -eq 'POST') {
    # Pushing the first gh-pages branch can enable Pages before has_pages updates.
    & gh api --method PUT "repos/$Repository/pages" --input $config --silent
}
if ($LASTEXITCODE -ne 0) { throw 'Could not configure GitHub Pages.' }
& gh api --method POST "repos/$Repository/pages/builds" --silent
if ($LASTEXITCODE -ne 0) { throw 'Could not request Pages deployment.' }
& gh api "repos/$Repository/pages" --jq '.html_url'
