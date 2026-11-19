$testRequest = @{
    message = "list all employees"
    sessionId = "test-session-123"
    context = @{}
} | ConvertTo-Json

Write-Host "Testing Chat API Endpoint..."
Write-Host "URL: http://localhost:5000/api/chat/message"
Write-Host "Request Body:"
Write-Host $testRequest
Write-Host ""
Write-Host "Sending request..."

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/chat/message" `
        -Method POST `
        -ContentType "application/json" `
        -Body $testRequest `
        -ErrorAction Stop

    Write-Host "? Response received:"
    Write-Host ($response | ConvertTo-Json -Depth 10)
} catch {
    Write-Host "? Error occurred:"
    Write-Host $_.Exception.Message
    if ($_.Exception.Response) {
        Write-Host "Response Status: " $_.Exception.Response.StatusCode
        $streamReader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Host "Response Body:" $streamReader.ReadToEnd()
    }
}
