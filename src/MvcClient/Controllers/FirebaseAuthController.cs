using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;

namespace MvcClient.Controllers;

/// <summary>
/// Handles OAuth2 Authorization Code flow for Firebase authentication.
/// This controller acts as a bridge between the IdentityServer and Firebase Auth.
/// </summary>
[Route("api/firebase-auth")]
[ApiController]
public class FirebaseAuthController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FirebaseAuthController> _logger;

    // OIDC/OAuth2 configuration
    private const string IdentityServerAuthority = "https://recruiting.ultipro.com:5001";
    private const string ClientId = "firebase-client";
    private const string RedirectUri = "https://localhost:5002/api/firebase-auth/callback";

    public FirebaseAuthController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<FirebaseAuthController> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the OAuth2 Authorization Code flow with PKCE.
    /// Redirects the user to IdentityServer for authentication.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        // Generate PKCE code verifier and challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Generate state for CSRF protection
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Store code verifier and state in session/cookie for later verification
        HttpContext.Session.SetString("code_verifier", codeVerifier);
        HttpContext.Session.SetString("oauth_state", state);
        HttpContext.Session.SetString("return_url", returnUrl ?? "/firebase/index.html");

        // Build authorization URL
        var authorizationUrl = $"{IdentityServerAuthority}/connect/authorize?" +
            $"client_id={ClientId}&" +
            $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString("openid profile email firebase")}&" +
            $"state={Uri.EscapeDataString(state)}&" +
            $"code_challenge={codeChallenge}&" +
            $"code_challenge_method=S256";

        _logger.LogInformation("Redirecting to IdentityServer for authentication");
        return Redirect(authorizationUrl);
    }

    /// <summary>
    /// OAuth2 callback endpoint. Exchanges authorization code for tokens,
    /// then mints a Firebase custom token.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        // Handle errors from IdentityServer
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("OAuth error: {Error} - {Description}", error, error_description);
            return BadRequest(new { error, error_description });
        }

        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new { error = "missing_code", error_description = "Authorization code is required" });
        }

        // Verify state to prevent CSRF
        var storedState = HttpContext.Session.GetString("oauth_state");
        if (string.IsNullOrEmpty(storedState) || storedState != state)
        {
            _logger.LogWarning("State mismatch. Possible CSRF attack.");
            return BadRequest(new { error = "invalid_state", error_description = "State parameter mismatch" });
        }

        // Get code verifier for PKCE
        var codeVerifier = HttpContext.Session.GetString("code_verifier");
        if (string.IsNullOrEmpty(codeVerifier))
        {
            return BadRequest(new { error = "missing_verifier", error_description = "Code verifier not found in session" });
        }

        try
        {
            // Exchange authorization code for tokens
            var tokenResponse = await ExchangeCodeForTokens(code, codeVerifier);

            if (tokenResponse == null)
            {
                return BadRequest(new { error = "token_exchange_failed" });
            }

            // Extract user info from ID token or call userinfo endpoint
            var userInfo = await GetUserInfo(tokenResponse.AccessToken);

            // Mint Firebase custom token
            var firebaseToken = await MintFirebaseCustomToken(userInfo);

            // Clear session
            HttpContext.Session.Remove("code_verifier");
            HttpContext.Session.Remove("oauth_state");

            var returnUrl = HttpContext.Session.GetString("return_url") ?? "/firebase/index.html";
            HttpContext.Session.Remove("return_url");

            // Return HTML page that sends the token to the Firebase app
            return Content(GenerateCallbackHtml(firebaseToken, userInfo, returnUrl), "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token exchange");
            return BadRequest(new { error = "token_exchange_error", error_description = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint to exchange auth code for Firebase token (for SPA/AJAX usage).
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> ExchangeToken([FromBody] TokenExchangeRequest request)
    {
        if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.CodeVerifier))
        {
            return BadRequest(new { error = "invalid_request", error_description = "Code and code_verifier are required" });
        }

        try
        {
            // Exchange authorization code for tokens
            var tokenResponse = await ExchangeCodeForTokens(request.Code, request.CodeVerifier, request.RedirectUri);

            if (tokenResponse == null)
            {
                return BadRequest(new { error = "token_exchange_failed" });
            }

            // Get user info
            var userInfo = await GetUserInfo(tokenResponse.AccessToken);

            // Mint Firebase custom token
            var firebaseToken = await MintFirebaseCustomToken(userInfo);

            return Ok(new
            {
                firebase_token = firebaseToken,
                user = userInfo,
                expires_in = 3600
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token exchange");
            return BadRequest(new { error = "token_exchange_error", error_description = ex.Message });
        }
    }

    private async Task<TokenResponse?> ExchangeCodeForTokens(string code, string codeVerifier, string? redirectUri = null)
    {
        var client = _httpClientFactory.CreateClient("IdentityServer");

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri ?? RedirectUri,
            ["code_verifier"] = codeVerifier
        };

        var response = await client.PostAsync(
            $"{IdentityServerAuthority}/connect/token",
            new FormUrlEncodedContent(tokenRequest));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Token exchange failed: {StatusCode} - {Content}",
                response.StatusCode, errorContent);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private async Task<UserInfo> GetUserInfo(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("IdentityServer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync($"{IdentityServerAuthority}/connect/userinfo");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get user info, using default values");
            return new UserInfo { Sub = Guid.NewGuid().ToString() };
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UserInfo>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new UserInfo { Sub = Guid.NewGuid().ToString() };
    }

    private async Task<string> MintFirebaseCustomToken(UserInfo userInfo)
    {
        // Initialize Firebase Admin if not already done
        if (FirebaseApp.DefaultInstance == null)
        {
            var useEmulator = _configuration.GetValue<bool>("Firebase:UseEmulator", true);

            if (useEmulator)
            {
                // For emulator, we need to set the environment variable
                Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", "localhost:9099");

                // Use a dummy service account for emulator
                FirebaseApp.Create(new AppOptions
                {
                    ProjectId = "demo-oauth-firebase",
                    Credential = GoogleCredential.FromJson(@"{
                        ""type"": ""service_account"",
                        ""project_id"": ""demo-oauth-firebase"",
                        ""private_key_id"": ""1"",
                        ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC4DGvmKVho57i9\nShvC09rrsKqmNEnOES4nK/NJw9TEjFpsJ7YGG7AbV4Xg7ETXU0CFNJwwI0KikMCS\n//2giKJtNPrZ6pAXY1kPa6WaoU1tnu9fnvSEKFsuGXO0LTWRz1McOnTRCyN+DPLI\nw8K06lKyycQEnGb/0/cYUJKCW92jur7o5kIyLCNQXwGNrGp0b7Pq2GnLUWtwYVoq\nh/8TloR8+jOm0tTFu07UrWO1bViwDQh1BIzqaT12Kseu6jyYb9Mnw2TsRNMEYAgc\nqrO5DZmmaD/tGaFajsvLkQhdt+hcSG0ndKto7HcJaJi2dh3yN0fZO8ti/nBWIfpn\nHmJAQtppAgMBAAECggEATIbNrovueOAwznQsCtxwIKP6sNT5AzfdiugZZsiIhZke\nV+5DH1MP7K59ukJDdYzmPPzdHJ7srA+oIvdSDBgEAYiP7WA8vZzPgTvvRdZgDX6S\nZZ4AaZsmHorysy5BIfmkww4DWJBbF6RmptfO886iyDhgytIFecY27eNCp/VuuQ7O\nWJYJaBxFRifO/dxQeFoGIQaI0fFJcFuQ7zrlxAYj77QF2ZQN40N1h6sUPHpuZndL\nCGn4sCEKWkqLwXkVTzECBBF1X+YSWlSXN8EjhIBuv3UgVjnPKeeG7N2T3Ciw/a4X\nRqco/mwISflceQD6A7I6y14tKJuHc8qHhe/3DKb+AQKBgQDl0TWvsKQDrGirHc4d\nubKrqnMBHFsUCtovAmnBEXCcfbUUU2mK689KVNS342nGzqwvNMQWy8N2wXfimLJt\n2a5NBvCg7AaBycsnG4qVuoBafYO6KRVUCe1BBrA41QitGi/f/Ac+CqFSjOnIMIj/\nabMkTpzjqPVa0PV9WVid6YKqiQKBgQDNBFVe31kjW1BYTNopvPNr/hCT/NKl60vS\nIIKaiRdrmMH16yu9oqturya9hhTUJAaXTeu2c8h0xfnqtFtnx2VaQ7z5wyWWsB3a\n225vv6WMRVJTywYS6+WRt6CQdjWxiAmC4pQUWZr9Ms7oZoj24dY0XIWvdxCnzIQI\naEit6vU44QKBgQC1drBRfcTEMcqj8vDhf9OYwQn2ApHYDYmiPOGMVVz59DibSBG6\nY+BV7Q3Z9XN8S4yh6aQ768D3cGRdQ/z/yDZdE/HE3xl0OgZzZsfS2mSnDxyIThBN\nP1lbUxCqj2w+YsMStUpyrqobKLEgJVLHeoq9TGWNTcgOYZi11Wqnpc5LIQKBgQCp\n48Fq0OJo7i5yPZ07oRyGjQ7n00YrwAQgqFgR/zCtNPTl+G9Swg4Vtob/3rA7626a\nyzNdCi0+tyAWYkashQtz9VYQEqp/aIoU5mlpqQJibr9+OGtcGqcuTWB81bhA2V4o\nW+Ihyu8oioXzB6TQEO0UjucpNB1VL6Dp5qDznhR/gQKBgBVRQgFNyhTCgxUjFoOJ\nWG2kLx9uqu9DXkyrYPTpJEcp0VjpyinWSTmHiZOQOenuo1mnqw21dYjh6mOtpG6G\nk9TM8zZhg/VOw7gmCSGKDphxEHrZYRp8LKaRlA92d90lDze8PCI2Fh7W/88EDk05\nKEenwfexy4Bxs+aDCcR6BphO\n-----END PRIVATE KEY-----\n"",
                        ""client_email"": ""firebase-adminsdk@demo-oauth-firebase.iam.gserviceaccount.com"",
                        ""client_id"": ""123456789"",
                        ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
                        ""token_uri"": ""https://oauth2.googleapis.com/token""
                    }")
                });

                _logger.LogInformation("Firebase Admin initialized with emulator");
            }
            else
            {
                // For production, use actual service account
                var serviceAccountPath = _configuration["Firebase:ServiceAccountPath"];
                if (!string.IsNullOrEmpty(serviceAccountPath))
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(serviceAccountPath)
                    });
                }
                else
                {
                    // Use Application Default Credentials
                    FirebaseApp.Create();
                }

                _logger.LogInformation("Firebase Admin initialized with service account");
            }
        }

        // Create custom claims from user info
        var claims = new Dictionary<string, object>
        {
            ["email"] = userInfo.Email ?? "",
            ["name"] = userInfo.Name ?? userInfo.PreferredUsername ?? "",
            ["email_verified"] = true,
            ["identity_provider"] = "ultipro",
            ["idp_sub"] = userInfo.Sub
        };

        // Create a unique Firebase UID based on the IdP subject
        var firebaseUid = $"ultipro_{userInfo.Sub}";

        // Mint the custom token
        var customToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(firebaseUid, claims);

        _logger.LogInformation("Minted Firebase custom token for user {UserId}", firebaseUid);

        return customToken;
    }

    private string GenerateCallbackHtml(string firebaseToken, UserInfo userInfo, string returnUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Complete</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .container {{
            background: white;
            padding: 2rem;
            border-radius: 10px;
            text-align: center;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
        }}
        .spinner {{
            width: 40px;
            height: 40px;
            border: 4px solid #f3f3f3;
            border-top: 4px solid #667eea;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin: 0 auto 1rem;
        }}
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='spinner'></div>
        <h3>Authentication Successful!</h3>
        <p>Welcome, {System.Web.HttpUtility.HtmlEncode(userInfo.Name ?? userInfo.Email ?? "User")}</p>
        <p>Signing you into Firebase...</p>
    </div>
    <script>
        // Send the Firebase token to the parent window or redirect
        const firebaseToken = '{firebaseToken}';
        const userInfo = {JsonSerializer.Serialize(userInfo)};

        if (window.opener) {{
            // Popup flow - send to opener
            window.opener.postMessage({{
                type: 'FIREBASE_TOKEN',
                token: firebaseToken,
                user: userInfo
            }}, '*');
            window.close();
        }} else if (window.parent !== window) {{
            // Iframe flow - send to parent
            window.parent.postMessage({{
                type: 'FIREBASE_TOKEN',
                token: firebaseToken,
                user: userInfo
            }}, '*');
        }} else {{
            // Redirect flow - store token and redirect
            sessionStorage.setItem('firebase_custom_token', firebaseToken);
            sessionStorage.setItem('firebase_user_info', JSON.stringify(userInfo));
            window.location.href = '{returnUrl}';
        }}
    </script>
</body>
</html>";
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class TokenExchangeRequest
{
    public string? Code { get; set; }
    public string? CodeVerifier { get; set; }
    public string? RedirectUri { get; set; }
}

public class TokenResponse
{
    public string? AccessToken { get; set; }
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
}

public class UserInfo
{
    public string Sub { get; set; } = "";
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? PreferredUsername { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
}
