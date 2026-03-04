@echo off
chcp 65001 >nul
title UABEA Bundle 批量导出工具

:: ============================================
:: 配置区域 - 修改以下路径
:: ============================================

:: UABEA 路径（根据你的实际路径修改）
set "UABEA=D:\DCC\Extractor\UABEA-M\UABEAvalonia\bin\x64\Release\net8.0\UABEAvalonia.exe"

:: 输入路径（可以是单个 Bundle 文件，或包含多个 Bundle 的文件夹）
set "INPUT_PATH=H:\HDownload\POPUCOM-AnkerGames\POPUCOM\Popucom_Data\StreamingAssets\BuildinFiles"

:: 输出路径（留空则使用输入路径\exported）
set "OUTPUT_PATH=D:\DCC\Extract\POPUCOM Extract"

:: ============================================
:: 主程序
:: ============================================

echo ==========================================
echo    UABEA Bundle 批量导出工具
echo ==========================================
echo.

:: 检查 UABEA 是否存在
if not exist "%UABEA%" (
    echo [错误] 找不到 UABEAvalonia.exe！
    echo 请修改脚本中的 UABEA 路径
    echo 当前路径: %UABEA%
    pause
    exit /b 1
)

:: 检查输入是否存在
if not exist "%INPUT_PATH%" (
    echo [错误] 输入路径不存在！
    echo %INPUT_PATH%
    pause
    exit /b 1
)

:: 设置默认输出路径
if "%OUTPUT_PATH%"=="" (
    if exist "%INPUT_PATH%\." (
        :: 输入是文件夹
        set "OUTPUT_PATH=%INPUT_PATH%\exported"
    ) else (
        :: 输入是文件
        set "OUTPUT_PATH=%~dp1exported"
    )
)

:: 创建输出目录
if not exist "%OUTPUT_PATH%" mkdir "%OUTPUT_PATH%"

echo [信息] 配置确认：
echo   UABEA: %UABEA%
echo   输入:  %INPUT_PATH%
echo   输出:  %OUTPUT_PATH%
echo.

:: 判断输入是文件还是文件夹
if exist "%INPUT_PATH%\." (
    echo [模式] 批量处理文件夹...
    set "IS_FOLDER=1"
) else (
    echo [模式] 处理单个文件...
    set "IS_FOLDER=0"
)
echo.

:: 执行导出
echo [开始] 正在导出 Bundle...
echo ------------------------------------------

"%UABEA%" batchexportbundle "%INPUT_PATH%" "%OUTPUT_PATH%"

echo.
echo ------------------------------------------
if %ERRORLEVEL%==0 (
    echo [完成] 导出成功！
    echo [输出] %OUTPUT_PATH%
    
    :: 统计导出的文件
    for /f %%A in ('dir /b "%OUTPUT_PATH%\*.*" 2^>nul ^| find /c /v ""') do (
        echo [统计] 共导出 %%A 个文件
    )
) else (
    echo [错误] 导出失败！错误代码: %ERRORLEVEL%
)

echo.
pause