[CmdletBinding()]
param(
    [switch]$SkipCoverage
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'Exercicio.sln'
$testProjectPath = Join-Path $repositoryRoot 'Questao5.Tests\Questao5.Tests.csproj'
$nugetConfigPath = Join-Path $repositoryRoot 'NuGet.Config'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar: dotnet $($Arguments -join ' ')"
    }
}

Write-Host '1/7 Restore bloqueado'
Invoke-DotNet @(
    'restore', $solutionPath,
    '--locked-mode',
    '--configfile', $nugetConfigPath,
    '--verbosity', 'minimal'
)

Write-Host '2/7 Auditoria de dependências diretas e transitivas'
Invoke-DotNet @(
    'package', 'list',
    '--project', $solutionPath,
    '--vulnerable',
    '--include-transitive',
    '--configfile', $nugetConfigPath,
    '--no-restore'
)

Write-Host '3/7 Secret scanning local dos arquivos rastreados'
& (Join-Path $PSScriptRoot 'Test-TrackedSecrets.ps1')

if ($LASTEXITCODE -ne 0) {
    throw 'Falha no secret scanning local.'
}

Write-Host '4/7 Verificação de formatação e analisadores'
Invoke-DotNet @(
    'format', $solutionPath,
    '--verify-no-changes',
    '--no-restore',
    '--verbosity', 'minimal'
)

Write-Host '5/7 Build Release determinístico em modo CI'
Invoke-DotNet @(
    'build', $solutionPath,
    '--configuration', 'Release',
    '--no-restore',
    '--verbosity', 'minimal',
    '-p:ContinuousIntegrationBuild=true'
)

Write-Host '6/7 Testes de regressão e segurança'
Invoke-DotNet @(
    'test', $solutionPath,
    '--configuration', 'Release',
    '--no-build',
    '--no-restore',
    '--verbosity', 'normal'
)

if (-not $SkipCoverage) {
    Write-Host '7/7 Cobertura compatível com Microsoft Testing Platform'
    Push-Location $repositoryRoot

    try {
        Invoke-DotNet @(
            'test', $testProjectPath,
            '--configuration', 'Release',
            '--no-build',
            '--no-restore',
            '--coverlet',
            '--coverlet-output-format', 'cobertura',
            '--coverlet-include', '[Questao5]*',
            '--coverlet-exclude', '[Questao5.Tests]*',
            '--verbosity', 'normal'
        )
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host '7/7 Cobertura ignorada por parâmetro explícito.'
}

Write-Host 'Gate de segurança concluído com sucesso.'