<#
    verify-bindpose-math.ps1 - offline proof of the bind-pose formula used by ModelApplier.

    No Unity, no game. Pure 4x4 arithmetic, so the fix can be checked on any machine.

    The claim being checked
    -----------------------
    ModelApplier moves a pack's vertices into the target space with a rigid transform Fix:

        v' = Fix * v

    For a bone slot whose binding is not found in the target skeleton's table it has to build that
    slot's binding inverse in the SAME space. Writing Wm for the model bone's world transform and
    Wt for the target bone's, and remembering that Fix maps model space onto target space, so
    Wt = Fix * Wm:

        correct : newBindpose = Wt^-1        = Wm^-1 * Fix^-1 = oldBindpose * Fix^-1
        shipped : newBindpose = Fix * oldBindpose            (the old, wrong order)

    The two agree only when Fix and oldBindpose commute, which in general they do not.

    Usage:  powershell -File tools\verify-bindpose-math.ps1
#>

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- 4x4 row-major helpers
function New-Matrix { , (New-Object 'double[]' 16) }

function Get-Mul([double[]]$a, [double[]]$b) {
    $r = New-Object 'double[]' 16
    for ($i = 0; $i -lt 4; $i++) {
        for ($j = 0; $j -lt 4; $j++) {
            $s = 0.0
            for ($k = 0; $k -lt 4; $k++) { $s += $a[$i * 4 + $k] * $b[$k * 4 + $j] }
            $r[$i * 4 + $j] = $s
        }
    }
    return , $r
}

# A rigid transform from a 3x3 rotation (flat, row-major) and a translation.
function Get-Rigid([double[]]$r9, [double[]]$t3) {
    $m = New-Object 'double[]' 16
    for ($i = 0; $i -lt 3; $i++) {
        for ($j = 0; $j -lt 3; $j++) { $m[$i * 4 + $j] = $r9[$i * 3 + $j] }
        $m[$i * 4 + 3] = $t3[$i]
    }
    $m[15] = 1.0
    return , $m
}

function Get-Inverse([double[]]$m) {
    # rigid only: R^-1 = R^T, t^-1 = -R^T t
    $r = New-Object 'double[]' 9
    for ($i = 0; $i -lt 3; $i++) { for ($j = 0; $j -lt 3; $j++) { $r[$i * 3 + $j] = $m[$j * 4 + $i] } }
    $t = New-Object 'double[]' 3
    for ($i = 0; $i -lt 3; $i++) {
        $s = 0.0
        for ($k = 0; $k -lt 3; $k++) { $s += $r[$i * 3 + $k] * $m[$k * 4 + 3] }
        $t[$i] = -$s
    }
    return , (Get-Rigid $r $t)
}

# How far a matrix is from the identity, as the largest absolute entry difference.
function Get-IdentityError([double[]]$m) {
    $worst = 0.0
    for ($i = 0; $i -lt 4; $i++) {
        for ($j = 0; $j -lt 4; $j++) {
            $want = if ($i -eq $j) { 1.0 } else { 0.0 }
            $d = [math]::Abs($m[$i * 4 + $j] - $want)
            if ($d -gt $worst) { $worst = $d }
        }
    }
    return $worst
}

function Format-Matrix([double[]]$m) {
    $lines = @()
    for ($i = 0; $i -lt 4; $i++) {
        $cells = @()
        for ($j = 0; $j -lt 4; $j++) { $cells += ('{0,9:F4}' -f $m[$i * 4 + $j]) }
        $lines += '    ' + ($cells -join ' ')
    }
    return ($lines -join "`n")
}

# ---------------------------------------------------------------- the case
# A model bone, sitting somewhere plausible on a body, and rotated.
$boneRot = Get-Rigid @(0.6, -0.8, 0.0,
                       0.8,  0.6, 0.0,
                       0.0,  0.0, 1.0) @(0.12, 1.05, -0.03)

# Fix mirrors what AutoAlign.Solve produces: model space mapped onto target space.
$fix = Get-Rigid @(0.0, 0.0, 1.0,
                   1.0, 0.0, 0.0,
                   0.0, 1.0, 0.0) @(0.0, 0.15, 0.0)

$oldBindpose = Get-Inverse $boneRot        # the pack's own bind pose for that bone
$targetWorld = Get-Mul $fix $boneRot       # where that bone ends up in target space

$correct = Get-Mul $oldBindpose (Get-Inverse $fix)
$shipped = Get-Mul $fix $oldBindpose

$errCorrect = Get-IdentityError (Get-Mul $targetWorld $correct)
$errShipped = Get-IdentityError (Get-Mul $targetWorld $shipped)

Write-Host "verify-bindpose-math" -ForegroundColor Cyan
Write-Host ""
Write-Host "The skinning identity  boneWorld * bindpose  must come out as the identity,"
Write-Host "otherwise the vertex is not where its bind pose says it is."
Write-Host ""
Write-Host ("  correct  oldBindpose * Fix^-1   ->  residual {0:E3}" -f $errCorrect) -ForegroundColor Green
Write-Host ("  shipped  Fix * oldBindpose      ->  residual {0:F4}" -f $errShipped) -ForegroundColor Yellow
Write-Host ""

Write-Host "  correct  boneWorld * (oldBindpose * Fix^-1):"
Write-Host (Format-Matrix (Get-Mul $targetWorld $correct))
Write-Host "  shipped  boneWorld * (Fix * oldBindpose):"
Write-Host (Format-Matrix (Get-Mul $targetWorld $shipped))
Write-Host ""

$pass = ($errCorrect -lt 1e-9) -and ($errShipped -gt 0.1)
if ($pass) {
    Write-Host "PASS: the corrected order satisfies the identity exactly, and the old order does not." -ForegroundColor Green
    Write-Host "      A vertex skinned through the old expression is displaced by up to $('{0:F3}' -f $errShipped) scene units." -ForegroundColor Green
} else {
    Write-Host "FAIL: the numbers do not show what the fix claims." -ForegroundColor Red
}
if (-not $pass) { exit 1 }
