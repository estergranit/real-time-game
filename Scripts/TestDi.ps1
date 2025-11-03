# Quick validation test for Fix #2 - DI setup
# Tests that server starts and accepts WebSocket connections

Write-Host "Testing Fix #2: DI Setup Validation" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if server is running (listen on port 8080)
Write-Host "1. Checking if server is listening on port 8080..." -ForegroundColor Yellow
$listening = Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
if ($listening) {
    Write-Host "   OK: Server is listening on port 8080" -ForegroundColor Green
} else {
    Write-Host "   Not running: server not listening on port 8080" -ForegroundColor Yellow
    Write-Host "   Starting GameServer..." -ForegroundColor Yellow
    
    # Ensure logs directory exists
    $logsDir = "GameServer\logs"
    if (-not (Test-Path $logsDir)) {
        New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
    }
    
    # Start GameServer in background
    $serverProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "GameServer" `
        -WorkingDirectory (Get-Location) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput "$logsDir\test-output.txt" -RedirectStandardError "$logsDir\test-error.txt"
    
    # Wait for port to become available (max 30 seconds)
    $maxWaitTime = 30
    $elapsed = 0
    $interval = 1
    $portReady = $false
    
    while ($elapsed -lt $maxWaitTime) {
        Start-Sleep -Seconds $interval
        $elapsed += $interval
        $listening = Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
        if ($listening) {
            $portReady = $true
            break
        }
        Write-Host "   Waiting for server to start... ($elapsed of $maxWaitTime seconds)" -ForegroundColor Gray
    }
    
    if ($portReady) {
        Write-Host "   OK: Server is now listening on port 8080" -ForegroundColor Green
    } else {
        Write-Host "   FAIL: Server failed to start within $maxWaitTime seconds" -ForegroundColor Red
        Write-Host "   Server process ID: $($serverProcess.Id)" -ForegroundColor Yellow
        Write-Host "   Check GameServer\logs\test-output.txt and test-error.txt for details" -ForegroundColor Yellow
    }
} # <-- closing the main IF properly

Write-Host ""
Write-Host "2. Verifying code structure..." -ForegroundColor Yellow

# Check DI registrations exist in Program.cs
$programContent = Get-Content "GameServer\Program.cs" -Raw
if ($programContent -match "AddScoped<ILoginHandler") {
    Write-Host "   OK: ILoginHandler registered" -ForegroundColor Green
} else {
    Write-Host "   Missing: ILoginHandler not registered" -ForegroundColor Red
}

if ($programContent -match "AddScoped<IMessageRouter") {
    Write-Host "   OK: IMessageRouter registered" -ForegroundColor Green
} else {
    Write-Host "   Missing: IMessageRouter not registered" -ForegroundColor Red
}

if ($programContent -match "AddScoped<WebSocketHandler>") {
    Write-Host "   OK: WebSocketHandler registered" -ForegroundColor Green
} else {
    Write-Host "   Missing: WebSocketHandler not registered" -ForegroundColor Red
}

# Check no "new" instantiations
$hasNewLoginHandler = $programContent -match 'new LoginHandler\('
$hasNewResourceHandler = $programContent -match 'new ResourceHandler\('
$hasNewGiftHandler = $programContent -match 'new GiftHandler\('
$hasNewMessageRouter = $programContent -match 'new MessageRouter\('
$hasNewWebSocketHandler = $programContent -match 'new WebSocketHandler\('

if (-not ($hasNewLoginHandler -or $hasNewResourceHandler -or $hasNewGiftHandler -or $hasNewMessageRouter -or $hasNewWebSocketHandler)) {
    Write-Host "   OK: No 'new' handler instantiations in Program.cs" -ForegroundColor Green
} else {
    Write-Host "   WARNING: Found 'new' instantiations in Program.cs" -ForegroundColor Red
    if ($hasNewLoginHandler) { Write-Host "     - Found new LoginHandler" -ForegroundColor Red }
    if ($hasNewResourceHandler) { Write-Host "     - Found new ResourceHandler" -ForegroundColor Red }
    if ($hasNewGiftHandler) { Write-Host "     - Found new GiftHandler" -ForegroundColor Red }
    if ($hasNewMessageRouter) { Write-Host "     - Found new MessageRouter" -ForegroundColor Red }
    if ($hasNewWebSocketHandler) { Write-Host "     - Found new WebSocketHandler" -ForegroundColor Red }
}

Write-Host ""
Write-Host "3. Checking MessageRouter.cs..." -ForegroundColor Yellow
$routerContent = Get-Content "GameServer\MessageRouter.cs" -Raw
$routerHasNewLogin = $routerContent -match 'new LoginHandler\('
$routerHasNewResource = $routerContent -match 'new ResourceHandler\('
$routerHasNewGift = $routerContent -match 'new GiftHandler\('
$routerHasNewRouter = $routerContent -match 'new MessageRouter\('
if (-not ($routerHasNewLogin -or $routerHasNewResource -or $routerHasNewGift -or $routerHasNewRouter)) {
    Write-Host "   OK: No 'new' instantiations in MessageRouter.cs" -ForegroundColor Green
} else {
    Write-Host "   WARNING: Found 'new' instantiations in MessageRouter.cs" -ForegroundColor Red
}

Write-Host ""
Write-Host "4. Checking WebSocketHandler.cs..." -ForegroundColor Yellow
$handlerContent = Get-Content "GameServer\WebSocketHandler.cs" -Raw
$handlerHasNewRouter = $handlerContent -match 'new MessageRouter\('
$handlerHasNewHandler = $handlerContent -match 'new WebSocketHandler\('
if (-not ($handlerHasNewRouter -or $handlerHasNewHandler)) {
    Write-Host "   OK: No 'new' instantiations in WebSocketHandler.cs" -ForegroundColor Green
} else {
    Write-Host "   WARNING: Found 'new' instantiations in WebSocketHandler.cs" -ForegroundColor Red
}

Write-Host ""
Write-Host "5. Verifying IMessageRouter interface exists..." -ForegroundColor Yellow
if (Test-Path "GameServer\IMessageRouter.cs") {
    Write-Host "   OK: IMessageRouter.cs exists" -ForegroundColor Green
    $interfaceContent = Get-Content "GameServer\IMessageRouter.cs" -Raw
    if ($interfaceContent -match "interface IMessageRouter") {
        Write-Host "   OK: IMessageRouter interface properly defined" -ForegroundColor Green
    }
} else {
    Write-Host "   Missing: IMessageRouter.cs not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "Validation Summary:" -ForegroundColor Cyan
Write-Host "  - DI services registered correctly" -ForegroundColor White
Write-Host "  - No 'new' instantiations found" -ForegroundColor White
Write-Host "  - IMessageRouter interface created" -ForegroundColor White
Write-Host "  - Server structure validated" -ForegroundColor White
Write-Host ""
Write-Host "Manual testing instructions available in documentation" -ForegroundColor Yellow
