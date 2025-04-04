using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using Microsoft.UI.Dispatching;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace DrinkDb_Auth.OAuthProviders
{
    /// <summary>
    /// A PKCE-based OAuth 2.0 flow for Twitter in a WinUI desktop app.
    /// </summary>
    public class TwitterOAuth2Provider : GenericOAuth2Provider
    {
        // ▼▼▼ 1) Set these appropriately ▼▼▼

        // In "Native App" flows, we typically do NOT use a Client Secret
        // but if you still have one in your config, you can read it; just don't send it.
        private string ClientId { get; }
        private string ClientSecret { get; } // not used if truly "native"

        // The same Callback/Redirect URI you registered in Twitter Developer Portal.
        // e.g. "http://127.0.0.1:5000/x-callback"
        private const string RedirectUri = "http://127.0.0.1:5000/x-callback";

        // Twitter endpoints:
        private const string AuthorizationEndpoint = "https://twitter.com/i/oauth2/authorize";
        private const string TokenEndpoint = "https://api.twitter.com/2/oauth2/token";
        private const string UserInfoEndpoint = "https://api.twitter.com/2/users/me";

        // Example scopes. If you want refresh tokens, include "offline.access".
        private readonly string[] Scopes = { "tweet.read", "users.read" };

        // Private fields for PKCE
        private string _codeVerifier = string.Empty;

        private readonly HttpClient _httpClient;

        public TwitterOAuth2Provider()
        {
            _httpClient = new HttpClient();

            // Load from config (if you wish):
            ClientId = System.Configuration.ConfigurationManager.AppSettings["TwitterClientId"] ?? "YOUR_CLIENT_ID";
            ClientSecret = System.Configuration.ConfigurationManager.AppSettings["TwitterClientSecret"] ?? "YOUR_CLIENT_SECRET";

            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientId: {ClientId}");
            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientSecret: {ClientSecret.Substring(0, Math.Min(4, ClientSecret.Length))}... (not used in PKCE)");
        }

        /// <summary>
        /// Optional method to quickly verify a stored token (not used in this flow).
        /// </summary>
        public AuthResponse Authenticate(string userId, string token)
        {
            return new AuthResponse
            {
                AuthSuccessful = !string.IsNullOrEmpty(token),
                SessionToken = token,
                NewAccount = false
            };
        }

        /// <summary>
        /// Generates the full authorization URL with PKCE code challenge and needed query params.
        /// </summary>
        public string GetAuthorizationUrl()
        {
            // 2) PKCE: Generate a code_verifier & code_challenge
            var (codeVerifier, codeChallenge) = GeneratePkceData();
            _codeVerifier = codeVerifier;  // store for later use in token request

            var scopeString = string.Join(" ", Scopes);

            var queryParameters = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "response_type", "code" },
                { "scope", scopeString },
                { "state", Guid.NewGuid().ToString() },

                // PKCE
                { "code_challenge", codeChallenge },
                { "code_challenge_method", "S256" }
            };

            // Build the query string
            var queryString = string.Join("&", queryParameters
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

            var authUrl = $"{AuthorizationEndpoint}?{queryString}";
            System.Diagnostics.Debug.WriteLine($"Generated authorization URL: {authUrl}");
            return authUrl;
        }

        /// <summary>
        /// When we get the code back from Twitter, exchange it for an access token.
        /// PKCE: We do NOT pass a client_secret, but we DO pass the same code_verifier we generated earlier.
        /// </summary>
        public async Task<AuthResponse> ExchangeCodeForTokenAsync(string code)
        {
            // 3) PKCE: Provide the stored code_verifier in the token request
            var tokenRequestParameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" },
                { "code_verifier", _codeVerifier }, // crucial for PKCE
            };

            System.Diagnostics.Debug.WriteLine("Exchanging code for token (PKCE).");
            foreach (var kv in tokenRequestParameters)
            {
                System.Diagnostics.Debug.WriteLine($"  {kv.Key}: {kv.Value}");
            }

            try
            {
                using var content = new FormUrlEncodedContent(tokenRequestParameters);
                var tokenResponse = await _httpClient.PostAsync(TokenEndpoint, content);
                var responseContent = await tokenResponse.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"Token Response status: {tokenResponse.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Token Response content: {responseContent}");

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("Token request failed with non-success status.");
                    return new AuthResponse {
                        AuthSuccessful = false,
                        SessionToken = string.Empty,
                        NewAccount = false
                    };
                }

                // Deserialize token response
                TwitterTokenResponse? tokenResult;
                try
                {
                    tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TwitterTokenResponse>();
                }
                catch
                {
                    // fallback if ReadFromJsonAsync fails
                    tokenResult = System.Text.Json.JsonSerializer.Deserialize<TwitterTokenResponse>(responseContent);
                }

                if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
                {
                    System.Diagnostics.Debug.WriteLine("No access token in tokenResult.");
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        SessionToken = string.Empty,
                        NewAccount = false
                    };
                }

                // 4) Optionally, get user info
                try
                {
                    using var userInfoClient = new HttpClient();
                    userInfoClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

                    var userResp = await userInfoClient.GetAsync(UserInfoEndpoint);
                    var userBody = await userResp.Content.ReadAsStringAsync();

                    if (!userResp.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"User info request failed. Response: {userBody}");
                        // We still have a valid token though
                        return new AuthResponse
                        {
                            AuthSuccessful = false,
                            SessionToken = tokenResult.AccessToken,
                            NewAccount = false
                        };
                    }

                    var userInfo = System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(userBody);
                    System.Diagnostics.Debug.WriteLine($"Authenticated user: {userInfo?.Email} ({userInfo?.Name})");

                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        SessionToken = tokenResult.AccessToken,
                        NewAccount = false
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception fetching user info: {ex.Message}");
                    // We'll still consider the token valid
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        SessionToken = tokenResult.AccessToken,
                        NewAccount = false
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExchangeCodeForTokenAsync exception: {ex.Message}");
                return new AuthResponse {AuthSuccessful = false, SessionToken = string.Empty, NewAccount = false };
            }
        }

        /// <summary>
        /// Shows a WebView, navigates to the Twitter OAuth page, intercepts the redirect to our local loopback.
        /// </summary>
        public async Task<AuthResponse> SignInWithTwitterAsync(Window parentWindow)
        {
            var tcs = new TaskCompletionSource<AuthResponse>();

            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Sign in with Twitter",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = parentWindow.Content.XamlRoot
                };

                var webView = new WebView2
                {
                    Width = 450,
                    Height = 600
                };
                dialog.Content = webView;

                // Initialize the WebView2
                await webView.EnsureCoreWebView2Async();

                // Listen for navigations
                webView.CoreWebView2.NavigationStarting += async (sender, args) =>
                {
                    var navUrl = args.Uri;
                    System.Diagnostics.Debug.WriteLine($"NavigationStarting -> {navUrl}");

                    // If it's the redirect back to our loopback, we parse out the code
                    if (navUrl.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase))
                    {
                        args.Cancel = true; // don't actually navigate to 127.0.0.1 in the WebView

                        var code = ExtractQueryParameter(navUrl, "code");
                        System.Diagnostics.Debug.WriteLine($"Found 'code' in callback: {code}");

                        var authResponse = await ExchangeCodeForTokenAsync(code);

                        // Close the dialog and return
                        parentWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            dialog.Hide();
                            tcs.SetResult(authResponse);
                        });
                    }
                };

                // Start the auth flow
                webView.CoreWebView2.Navigate(GetAuthorizationUrl());

                // Show the dialog
                var dialogResult = await dialog.ShowAsync();

                // If user closed the dialog manually before we got a code
                if (!tcs.Task.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("Dialog closed; no code was returned.");
                    tcs.SetResult(new AuthResponse {AuthSuccessful = false, SessionToken = string.Empty, NewAccount = false });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SignInWithTwitterAsync error: {ex.Message}");
                tcs.TrySetException(ex);
            }

            return await tcs.Task;
        }

        /// <summary>
        /// Helper: parse one query param (e.g. ?code=xxx) from a URL
        /// </summary>
        private string ExtractQueryParameter(string url, string paramName)
        {
            var uri = new Uri(url);
            var query = uri.Query.TrimStart('?');
            var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0] == paramName)
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
            throw new ArgumentException($"Parameter '{paramName}' not found in URL: {url}", nameof(url));
        }

        /// <summary>
        /// Generate PKCE code_verifier (random) + code_challenge (SHA256).
        /// </summary>
        private (string codeVerifier, string codeChallenge) GeneratePkceData()
        {
            // code_verifier: a random 43–128 char string
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);

            // Base64Url-encode without padding
            var codeVerifier = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // code_challenge: SHA256 hash of verifier, then Base64Url-encode
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                var codeChallenge = Convert.ToBase64String(hash)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');

                return (codeVerifier, codeChallenge);
            }
        }

        /// <summary>
        /// If Twitter provides an ID token, you could parse it here. Typically, Twitter doesn't.
        /// </summary>
        private TwitterUserInfoResponse ExtractUserInfoFromIdToken(string idToken)
        {
            var parts = idToken.Split('.');
            if (parts.Length != 3)
            {
                throw new ArgumentException("Invalid ID token format.", nameof(idToken));
            }

            var payload = parts[1];
            while (payload.Length % 4 != 0) payload += '=';
            var jsonBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var json = Encoding.UTF8.GetString(jsonBytes);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(json, options) ?? throw new Exception("Failed to deserialize ID token payload.");
        }
    }

    /// <summary>
    /// Model for the token response from Twitter
    /// Twitter typically returns access_token, token_type, expires_in, refresh_token if "offline.access"
    /// </summary>
    internal class TwitterTokenResponse
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public required string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public required string RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public required string IdToken { get; set; }
    }

    /// <summary>
    /// Model for user info from Twitter /2/users/me
    /// Fields depend on which you requested (like "email" requires special permission).
    /// </summary>
    internal class TwitterUserInfoResponse
    {
        [JsonPropertyName("sub")]
        public required string Sub { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("given_name")]
        public required string GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public required string FamilyName { get; set; }

        [JsonPropertyName("picture")]
        public required string Picture { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("email_verified")]
        public required bool EmailVerified { get; set; }

        [JsonPropertyName("locale")]
        public required string Locale { get; set; }
    }
}
