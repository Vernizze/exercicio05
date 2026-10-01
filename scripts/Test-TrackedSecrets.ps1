[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$trackedFiles = git -C $repositoryRoot -c core.quotepath=false ls-files --cached --others --exclude-standard

if ($LASTEXITCODE -ne 0) {
    throw 'Não foi possível obter a lista de arquivos versionados ou candidatos ao próximo commit.'
}

$textExtensions = @(
    '.cs', '.csproj', '.json', '.config', '.props', '.targets',
    '.ps1', '.yml', '.yaml', '.xml', '.env', '.md', '.txt'
)

$patterns = @(
    [pscustomobject]@{
        Name = 'chave privada'
        Regex = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    },
    [pscustomobject]@{
        Name = 'segredo atribuído em configuração'
        Regex = '(?im)["'']?(?:password|passwd|pwd|clientsecret|client_secret|apikey|api_key|access_token)["'']?\s*[:=]\s*["'']?(?!\s*(?:null|false|true|change[-_]?me|example|sample|placeholder)["'']?\s*$)[^\s,"'']{8,}'
    },
    [pscustomobject]@{
        Name = 'senha em connection string'
        Regex = '(?i)(?:Password|Pwd)\s*=\s*[^;\s]{4,}'
    }
)

$findings = [System.Collections.Generic.List[string]]::new()

foreach ($relativePath in $trackedFiles) {
    if ($relativePath -eq 'scripts/Test-TrackedSecrets.ps1') {
        continue
    }

    $absolutePath = Join-Path $repositoryRoot $relativePath
    $extension = [System.IO.Path]::GetExtension($relativePath)

    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf) -or
        $textExtensions -notcontains $extension.ToLowerInvariant()) {
        continue
    }

    $content = Get-Content -LiteralPath $absolutePath -Raw

    foreach ($pattern in $patterns) {
        if ($content -match $pattern.Regex) {
            $findings.Add("$relativePath`: possível $($pattern.Name)")
        }
    }
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Error $_ }
    throw 'A verificação encontrou possíveis segredos em arquivos rastreados.'
}

Write-Host 'Secret scanning local: nenhum padrão de segredo encontrado em arquivos versionados ou candidatos ao commit.'