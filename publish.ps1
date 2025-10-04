param(
    [switch]$NoSign,
    [switch]$Zip
)

$MSBuildExe="msbuild"
if ($null -eq (Get-Command $MSBuildExe -ErrorAction SilentlyContinue)) {
    $MSBuildExe="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    Write-Warning "MSBuild not in path, using $MSBuildExe"
    
    if ($null -eq (Get-Command $MSBuildExe -ErrorAction SilentlyContinue)) {
        Write-Error "Cannot find MSBuild (aborting)"
        exit 1
    }
}

if ($NoSign) {
    Write-Warning "Signing has been disabled."
}

if ($Zip) {
    Write-Host "Zip archive will be created!" -ForegroundColor Green
}

# Publish script for SRS-Recording projects
$outputPath = ".\install-build"

# Common publish parameters
$commonParams = @(
    "--configuration", "Release",
    "/p:PublishReadyToRun=true",
    "/p:PublishSingleFile=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:IncludeSourceRevisionInInformationalVersion=false" #Dont add a git hash into the build version
)

# Define the path to signtool.exe
$signToolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
if (-not $NoSign -and -not (Test-Path $signToolPath)) {
    Write-Error "SignTool.exe not found at $signToolPath. Please verify the path."
    exit 1
}

# Define common parameters for signtool
$commonParameters = @(
    "sign",                                     # The sign command for signtool
    "/n", "`"Open Source Developer, Shalev Ohad`"", # Subject Name of the certificate (explicitly quoted)
    "/a",                                       # Automatically select the best signing certificate
    "/t", "`"http://time.certum.pl/`"",             # Timestamp server URL (explicitly quoted)
    "/fd", "`"sha256`"",                              # File digest algorithm (explicitly quoted)
    "/v"                                        # Verbose output
)

# DCS-SRS-RecordingClient
Write-Host "Publishing DCS-SRS-RecordingClient..." -ForegroundColor Green
Remove-Item "$outputPath\DCS-SRS-RecordingClient" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./DCS-SRS-RecordingClient.UI/DCS-SRS-RecordingClient.UI.csproj"
dotnet publish "./DCS-SRS-RecordingClient.UI/DCS-SRS-RecordingClient.UI.csproj" `
    --runtime win-x64 `
    --output "$outputPath\DCS-SRS-RecordingClient" `
    --self-contained false `
    @commonParams
Remove-Item "$outputPath\DCS-SRS-RecordingClient\*.so"  -Recurse -ErrorAction SilentlyContinue
Remove-Item "$outputPath\DCS-SRS-RecordingClient\*.config"  -Recurse -ErrorAction SilentlyContinue


# DCS-SRS-RecordingClient Command Line - Windows
Write-Host "Publishing DCS-SRS-RecordingClient CommandLine for Windows..." -ForegroundColor Green
Remove-Item "$outputPath\RecordingClientCommandLine-Windows" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./DCS-SRS-RecordingClient.CLI\DCS-SRS-RecordingClient.CLI.csproj"
dotnet publish "./DCS-SRS-RecordingClient.CLI\DCS-SRS-RecordingClient.CLI.csproj" `
    --runtime win-x64 `
    --output "$outputPath\RecordingClientCommandLine-Windows" `
    --self-contained true `
    @commonParams
Remove-Item "$outputPath\RecordingClientCommandLine-Windows\*.so"  -Recurse -ErrorAction SilentlyContinue

# DCS-SRS-RecordingClient Command Line - Linux
Write-Host "Publishing DCS-SRS-RecordingClient CommandLine for Linux..." -ForegroundColor Green
Remove-Item "$outputPath\RecordingClientCommandLine-Linux" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./DCS-SRS-RecordingClient.CLI\DCS-SRS-RecordingClient.CLI.csproj"
dotnet publish "./DCS-SRS-RecordingClient.CLI\DCS-SRS-RecordingClient.CLI.csproj" `
    --runtime linux-x64 `
    --output "$outputPath\RecordingClientCommandLine-Linux" `
    --self-contained true `
    @commonParams
Remove-Item "$outputPath\RecordingClientCommandLine-Linux\*.dll"  -Recurse -ErrorAction SilentlyContinue

# VC Redist
Write-Host "Downloading VC redistributables..." -ForegroundColor Green
Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile "$outputPath\VC_redist.x64.exe"

Write-Host "Publishing complete! Check the $outputPath directory for the published files." -ForegroundColor Green

##Now Sign
Write-Host "Signing files"

if ($NoSign) {
    Write-Host "Skipped"
} else {
    # Define the root path to search for files to be signed
    # The script will recursively find all .dll and .exe files in this path and its subdirectories.
    $searchPath = $outputPath

    if (-not (Test-Path $searchPath -PathType Container)) {
        Write-Error "Search path '$searchPath' not found or is not a directory. Please verify the path."
        exit 1
    }

    Write-Host "Searching for .dll and .exe files in '$searchPath' and its subdirectories..."
    # Get all .exe files recursively. -File ensures we only get files.
    try {
        $filesToSign = Get-ChildItem -Path $searchPath -Recurse -Include "srs.dll", "*.exe" -File -ErrorAction Stop
    } catch {
        Write-Error "Error occurred while searching for files: $($_.Exception.Message)"
        exit 1
    }


    if ($null -eq $filesToSign -or $filesToSign.Count -eq 0) {
        Write-Warning "No .exe files found in '$searchPath' to sign."
    } else {
        Write-Host "Found $($filesToSign.Count) file(s) to process."

        # Loop through each found file and sign it
        foreach ($fileInstance in $filesToSign) {
            $filePath = $fileInstance.FullName # Get the full path of the file

            if ($fileInstance.FullName -match "VC_redist.x64")
            {
                Write-Host "Skipping VCRedist " -ForegroundColor Green
                continue
            }

            Write-Host "Attempting to sign $filePath..."

            # Explicitly quote the file path argument for signtool
            $quotedFilePath = "`"$filePath`""

            # Construct the arguments for the current file.
            # The explicitly quoted file path is added as the last argument.
            $currentFileArgs = $commonParameters + $quotedFilePath

            # Start the signing process
            # Using Start-Process to call external executables is a robust way.
            # -NoNewWindow keeps the output in the current console.
            # -Wait ensures PowerShell waits for signtool.exe to complete.
            # -PassThru returns a process object (useful for checking ExitCode).
            try {
                $process = Start-Process -FilePath $signToolPath -ArgumentList $currentFileArgs -Wait -PassThru -NoNewWindow -ErrorAction Stop
                
                Write-Host "Start-Process -FilePath $signToolPath -ArgumentList $currentFileArgs -Wait -PassThru -NoNewWindow -ErrorAction Stop"
                
                if ($process.ExitCode -eq 0) {
                    Write-Host "Successfully signed $filePath." -ForegroundColor Green
                } else {
                    # Signtool.exe usually outputs its own errors to stderr, which PowerShell might show.
                    Write-Error "Failed to sign $filePath. SignTool.exe exited with code: $($process.ExitCode). Check output above for details from SignTool."
                    
                    exit 1;
                }
            } catch {
                Write-Error "An error occurred while trying to run SignTool.exe for $filePath. Error: $($_.Exception.Message)"
            }
        }
    }
}

### Zip

Write-Host "Creating zip archive..." -ForegroundColor Green

if(!$Zip){
    Write-Warning "Skipped."
    exit 0
}


Write-Host "Removing old zip files from '$outputPath'..." -ForegroundColor Yellow
Remove-Item -Path "$outputPath\*.zip" -ErrorAction SilentlyContinue


$zipFileName = "DCS-SRS-RecordingClient.zip"
$zipFilePath = Join-Path -Path (Get-Item -Path $outputPath).FullName -ChildPath $zipFileName


try {
    Compress-Archive -Path "$outputPath\*" -DestinationPath $zipFilePath -Force
    Write-Host "Successfully created zip file at: $zipFilePath" -ForegroundColor Green
} catch {
    Write-Error "Failed to create the zip archive. Error: $($_.Exception.Message)"
    exit 1
}
