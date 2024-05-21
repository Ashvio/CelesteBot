@echo off
setlocal

:: Define current script directory
set "ScriptDir=%~dp0"
if "%ScriptDir:~-1%" == "\" set "ScriptDir=%ScriptDir:~0,-1%"

:: Define target directory
set "TargetDir=C:\Program Files (x86)\Steam\steamapps\common\Celeste"

:: Define nuget packages for current user
set "PythonnetDir=%USERPROFILE%\.nuget\packages\pythonnet\3.0.3\lib\netstandard2.0\Python.Runtime.dll"
set "DesktopRobotDir=%USERPROFILE%\.nuget\packages\desktop.robot\1.5.0\lib\net6.0\Desktop.Robot.dll"


:: Check if target directory exists, else create it
if not exist "%TargetDir%\Mods\CelesteBot\Code" (
    echo Target directory do not exist. Creation of required directories...
    mkdir "%TargetDir\Mods\CelesteBot\Code%"
    if %errorlevel% neq 0 (
        echo Erreur: Impossible to create target directory.
        exit /b 1
    )
)

:: Copy files
echo --- Copying %ScriptDir%\bin\Debug\net7.0\CelesteBot_2023.dll to %TargetDir%\Mods\CelesteBot\Code
copy "%ScriptDir%\bin\Debug\net7.0\CelesteBot_2023.dll" "%TargetDir%\Mods\CelesteBot\Code"
if %errorlevel% neq 0 (
    echo Error: Failed copying file: %ScriptDir%\bin\Debug\net7.0\CelesteBot_2023.dll
    exit /b 1
)

echo --- Copying %PythonnetDir% to %TargetDir%
copy "%PythonnetDir%" "%TargetDir%\"
if %errorlevel% neq 0 (
    echo Error: Failed copying file: %PythonnetDir%
    exit /b 1
)

echo --- Copying %DesktopRobotDir% to %TargetDir%
copy "%DesktopRobotDir%" "%TargetDir%\"
if %errorlevel% neq 0 (
    echo Error: Failed copying file: %DesktopRobotDir%
    exit /b 1
)

echo --- Copying %ScriptDir%\levels.csv to %TargetDir%
copy "%ScriptDir%\levels.csv" "%TargetDir%\"
if %errorlevel% neq 0 (
    echo Error: Failed copying file: %ScriptDir%\levels.csv
    exit /b 1
)

echo --- Copying %ScriptDir%\everest.yaml to %TargetDir%\Mods\CelesteBot
copy "%ScriptDir%\everest.yaml" "%TargetDir%\Mods\CelesteBot"
if %errorlevel% neq 0 (
    echo Error: Failed copying file: %ScriptDir%\everest.yaml
    exit /b 1
)

robocopy /MIR /IM /E "python_rl" "%TargetDir%\python_rl"

endlocal
exit /b 0

robocopy /MIR /IM /E "python_rl " "C:\Program Files (x86)\Steam\steamapps\common\Celeste\python_rl\ " 

endlocal
exit /b 0
