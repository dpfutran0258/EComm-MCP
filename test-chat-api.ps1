<#
.SYNOPSIS
    Sends one message to the chat endpoint (POST /api/chat/message) and prints the response.

.EXAMPLE
    ./test-chat-api.ps1
    ./test-chat-api.ps1 -Message "show employees in Engineering" -BaseUrl http://localhost:5000
#>
[CmdletBinding()]
param(
    [string]$Message = 'list all employees',
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$SessionId = [guid]::NewGuid().ToString()
)

$uri = "$BaseUrl/api/chat/message"

# Request shape matches EmployeeManagement.Application.DTOs.ChatRequest.
# preferredModel is optional and must be in AI:OpenRouter:AllowedModels, otherwise the API returns 400.
$body = @{
    message   = $Message
    sessionId = $SessionId
    context   = @{}
} | ConvertTo-Json -Depth 5

Write-Host "POST $uri"
Write-Host $body
Write-Host ''

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 75
    Write-Host 'Response:' -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10
}
catch {
    Write-Host "Request failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
        Write-Host "Response body: $($_.ErrorDetails.Message)"
    }
    exit 1
}
