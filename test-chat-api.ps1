<#
.SYNOPSIS
    Sends one message to the chat endpoint (POST /api/chat) and prints the response.

.DESCRIPTION
    The server is stateless: the conversation context is the "messages" array you send. This script sends a single
    user message. To continue a conversation, send the earlier messages (the "newMessages" of each reply) first.
    To confirm a delete, pass -ConfirmAction with the pendingAction.token from the previous reply.

.EXAMPLE
    ./test-chat-api.ps1
    ./test-chat-api.ps1 -Message "show employees in Engineering" -BaseUrl http://localhost:5000
    ./test-chat-api.ps1 -ConfirmAction "<pendingAction.token>"
#>
[CmdletBinding()]
param(
    [string]$Message = 'list all employees',
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$ConfirmAction
)

$uri = "$BaseUrl/api/chat"

# Request shape matches EmployeeManagement.Application.DTOs.ChatRequest.
# preferredModel is optional and must be in AI:OpenRouter:AllowedModels, otherwise the API returns 400.
if ($ConfirmAction) {
    $payload = @{ confirmAction = $ConfirmAction }
}
else {
    $payload = @{ messages = @(@{ role = 'user'; content = $Message }) }
}
$body = $payload | ConvertTo-Json -Depth 6

Write-Host "POST $uri"
Write-Host $body
Write-Host ''

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 130
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
