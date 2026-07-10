@echo off
setlocal enabledelayedexpansion

set APPNAME=PhieuLuuOngNuocCoop
set OUTEXE=%APPNAME%.exe

set VBC=
for %%v in (v4.0.30319 v4.0.30128 v4.0.21006 v4.0.20506) do (
    if exist "%WINDIR%\Microsoft.NET\Framework\%%v\vbc.exe" (
        set "VBC=%WINDIR%\Microsoft.NET\Framework\%%v\vbc.exe"
    )
)
if "%VBC%"=="" (
    if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\vbc.exe" (
        set "VBC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\vbc.exe"
    )
)

if "%VBC%"=="" (
    echo [LOI] Khong tim thay vbc.exe cho .NET Framework 4.x
    echo Kiem tra thu muc %WINDIR%\Microsoft.NET\Framework\ hoac Framework64\
    pause
    exit /b 1
)

echo Dung trinh bien dich: %VBC%
echo Dang build %OUTEXE% ...
echo.

"%VBC%" /nologo /target:winexe /out:%OUTEXE% /optimize+ /optionstrict+ /optionexplicit+ ^
    /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll ^
    Program.vb Form1.vb PlatformGame.vb NetworkPeer.vb

if errorlevel 1 (
    echo.
    echo [LOI] Build that bai.
    pause
    exit /b 1
) else (
    echo.
    echo [OK] Build thanh cong: %OUTEXE%
    echo [LUU Y] Nho tao thu muc "Assets" cung thu muc voi %OUTEXE% va bo cac
    echo          file PNG sprite vao (player0.png, player1.png, enemy_soldier.png,
    echo          enemy_boss.png, tile_ground.png, bullet_player.png, bullet_enemy.png,
    echo          powerup_weapon.png, powerup_life.png, background.png). Neu khong co,
    echo          game se tu dong fallback ve bang GDI+ hinh hoc.
)

pause
