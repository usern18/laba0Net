param(
	[Parameter(Mandatory = $true)]
	[string]$AccessToken,
	[string]$BaseUrl = "http://localhost:5000",
	[int]$ProductId = 1,
	[switch]$VerifyCreate
)

$ErrorActionPreference = "Stop"
$headers = @{ Authorization = "Bearer $AccessToken" }

function Assert-Status([string]$Uri, [int]$ExpectedStatus, [hashtable]$RequestHeaders = @{}) {
	try {
		$response = Invoke-WebRequest -Uri $Uri -Headers $RequestHeaders -TimeoutSec 10
		$status = [int]$response.StatusCode
		$body = $response.Content
	}
	catch {
		if (-not $_.Exception.Response) {
			throw "Request to $Uri failed before an HTTP response: $($_.Exception.Message)"
		}
		$status = [int]$_.Exception.Response.StatusCode
		$reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
		$body = $reader.ReadToEnd()
	}

	if ($status -ne $ExpectedStatus) {
		throw "Expected HTTP $ExpectedStatus from $Uri, received HTTP $status. Body: $body"
	}
	return $body
}

$null = Assert-Status "http://localhost:5001/health" 200
$null = Assert-Status "http://localhost:5002/health" 200
$null = Assert-Status "http://localhost:5003/health" 200
$null = Assert-Status "$BaseUrl/health" 200
$legacyCatalogJson = Assert-Status "$BaseUrl/api/catalog" 200
if ($null -eq ($legacyCatalogJson | ConvertFrom-Json)) {
	throw "The remaining-monolith catch-all route did not return a catalog response."
}
$catalogJson = Assert-Status "$BaseUrl/api/v1/catalog/items?page=1&pageSize=1" 200
$catalog = $catalogJson | ConvertFrom-Json
if ($null -eq $catalog.items -or $catalog.page -ne 1) {
	throw "The Catalog route did not return the expected paginated service response."
}
$null = Assert-Status "$BaseUrl/api/orders" 401
$userJson = Assert-Status "$BaseUrl/api/v1/users/me" 200 $headers
$ordersJson = Assert-Status "$BaseUrl/api/orders?page=1&pageSize=20" 200 $headers
$user = $userJson | ConvertFrom-Json
$orders = $ordersJson | ConvertFrom-Json
if ($user.id -le 0 -or $null -eq $orders.items -or $orders.page -ne 1) {
	throw "The Auth or Ordering route did not return the expected authenticated response."
}

if ($VerifyCreate) {
	$idempotencyKey = [guid]::NewGuid().ToString("N")
	$requestBody = @{ productId = $ProductId; quantity = 1 } | ConvertTo-Json -Compress
	$createHeaders = @{ Authorization = "Bearer $AccessToken"; "Idempotency-Key" = $idempotencyKey }
	try {
		$created = Invoke-WebRequest -Method Post -Uri "$BaseUrl/api/orders" -Headers $createHeaders `
			-ContentType "application/json" -Body $requestBody -TimeoutSec 10
	}
	catch {
		if (-not $_.Exception.Response) {
			throw "Order creation through the gateway failed before an HTTP response: $($_.Exception.Message)"
		}
		$status = [int]$_.Exception.Response.StatusCode
		$reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
		throw "Expected HTTP 201 from the ordering route, received HTTP $status. Body: $($reader.ReadToEnd())"
	}
	if ([int]$created.StatusCode -ne 201 -or -not $created.Headers.Location) {
		throw "Order creation did not return HTTP 201 and Location."
	}
	$firstOrder = $created.Content | ConvertFrom-Json
	$repeated = Invoke-WebRequest -Method Post -Uri "$BaseUrl/api/orders" -Headers $createHeaders `
		-ContentType "application/json" -Body $requestBody -TimeoutSec 10
	$repeatedOrder = $repeated.Content | ConvertFrom-Json
	if ([int]$repeated.StatusCode -ne 201 -or $firstOrder.id -ne $repeatedOrder.id) {
		throw "Repeated Idempotency-Key did not return the original order."
	}
	Write-Output "Gateway create verification passed for order ID $($firstOrder.id). This test order may be removed after verification."
}

Write-Output "Gateway smoke verification passed: readiness, Catalog, Auth, and protected Orders."
