using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Storage;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.UI.Dispatching;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GoogleOAuth2Provider : GenericOAuth2Provider
    {
        // These values should be loaded from configuration, not hard-coded
        private string ClientId { get; }
        private string ClientSecret { get; }
        private const string RedirectUri = "urn:ietf:wg:oauth:2.0:oob";
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";
        private readonly string[] Scopes = { "profile", "email" };

        private HttpClient _httpClient;

        public GoogleOAuth2Provider()
        {
            _httpClient = new HttpClient();
            
            // Load credentials from app configuration
            ClientId = System.Configuration.ConfigurationManager.AppSettings["GoogleClientId"] ?? "YOUR_CLIENT_ID";
            ClientSecret = System.Configuration.ConfigurationManager.AppSettings["GoogleClientSecret"] ?? "YOUR_CLIENT_SECRET";
            
            // Debug: Log the loaded credentials
            System.Diagnostics.Debug.WriteLine($"Loaded Google ClientId: {ClientId}");
            System.Diagnostics.Debug.WriteLine($"Loaded Google ClientSecret: {ClientSecret.Substring(0, 4)}..."); // Show only first few chars for security
            
            // Check if using default/placeholder credentials
            if (ClientId == "YOUR_CLIENT_ID" || ClientSecret == "YOUR_CLIENT_SECRET")
            {
                System.Diagnostics.Debug.WriteLine("WARNING: Using placeholder Google OAuth credentials. Authentication will fail!");
                System.Diagnostics.Debug.WriteLine("Please configure proper Google OAuth credentials in App.config");
            }
            
            if (ClientId.Contains("apps.googleusercontent.com") == false)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: Client ID doesn't have the expected format (ending with apps.googleusercontent.com)");
            }
        }

        public AuthResponse Authenticate(string userId, string token)
        {
            // This method is used for validating an existing token, but we'll focus on the initial auth flow
            var response = new AuthResponse
            {
                AuthSuccessful = !string.IsNullOrEmpty(token),
                OAuthToken = token,
                SessionId = Guid.Empty,
                NewAccount = false
            };

            return response;
        }

        public string GetAuthorizationUrl()
        {
            var scopeString = string.Join(" ", Scopes);
            
            var queryParameters = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "response_type", "code" },
                { "scope", scopeString },
                { "access_type", "offline" },
                { "state", Guid.NewGuid().ToString() }
            };

            var queryString = string.Join("&", queryParameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            var authUrl = $"{AuthorizationEndpoint}?{queryString}";
            
            System.Diagnostics.Debug.WriteLine($"Generated authorization URL: {authUrl}");
            return authUrl;
        }

        public async Task<AuthResponse> ExchangeCodeForTokenAsync(string code)
        {
            var tokenRequestParameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" }
            };

            // Debug: Log FULL request parameters (for troubleshooting only!)
            System.Diagnostics.Debug.WriteLine("Token request parameters (DO NOT SHARE THESE VALUES!):");
            System.Diagnostics.Debug.WriteLine($"  - code: {code}");
            System.Diagnostics.Debug.WriteLine($"  - client_id: {ClientId}");
            System.Diagnostics.Debug.WriteLine($"  - client_secret: {ClientSecret}"); // SECURITY NOTE: Only log this during development!
            System.Diagnostics.Debug.WriteLine($"  - redirect_uri: {RedirectUri}");
            System.Diagnostics.Debug.WriteLine($"  - grant_type: authorization_code");

            try
            {
                // Alternative approach using HttpClient with form content
                var content = new FormUrlEncodedContent(tokenRequestParameters);
                var tokenResponse = await _httpClient.PostAsync(TokenEndpoint, content);
                
                var responseContent = await tokenResponse.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Response status: {tokenResponse.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response content: {responseContent}");
                
                if (tokenResponse.IsSuccessStatusCode)
                {
                    try 
                    {
                        TokenResponse? tokenResult;
                        
                        try
                        {
                            // Try the default deserialization
                            tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
                            System.Diagnostics.Debug.WriteLine("Successfully deserialized token response");
                        }
                        catch (Exception jsonEx)
                        {
                            // If deserialization fails, try manual parsing
                            System.Diagnostics.Debug.WriteLine($"Automatic deserialization failed: {jsonEx.Message}");
                            System.Diagnostics.Debug.WriteLine("Attempting manual JSON parsing");
                            
                            try
                            {
                                // Parse the JSON manually
                                var options = new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                };
                                
                                tokenResult = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseContent, options) ?? throw new Exception("Failed to deserialize token response manually");
                            }
                            catch (Exception manualEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Manual parsing also failed: {manualEx.Message}");
                                throw; // Let the outer catch handle it
                            }
                        }
                        
                        // Debug the token values
                        if (tokenResult != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Token values: AccessToken={tokenResult.AccessToken?.Length ?? 0 } chars, " +
                                                             $"TokenType={tokenResult.TokenType}, " +
                                                             $"ExpiresIn={tokenResult.ExpiresIn}, " +
                                                             $"RefreshToken={tokenResult.RefreshToken?.Length ?? 0} chars");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Token result is null after both deserialization attempts");
                        }
                        
                        // Verify we got a valid access token
                        if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
                        {
                            System.Diagnostics.Debug.WriteLine("ERROR: Token result was null or access token was empty");
                            System.Diagnostics.Debug.WriteLine($"Raw response content: {responseContent}");
                            return new AuthResponse
                            {
                                AuthSuccessful = false,
                                OAuthToken = string.Empty,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }
                        
                        // Get user info with the access token
                        try
                        {
                            // Create a new HttpClient just for this request to ensure clean headers
                            using (var userInfoClient = new HttpClient())
                            {
                                // Set the authorization header correctly
                                userInfoClient.DefaultRequestHeaders.Authorization = 
                                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
                                
                                System.Diagnostics.Debug.WriteLine($"Fetching user info from {UserInfoEndpoint}");
                                System.Diagnostics.Debug.WriteLine($"Using access token: {tokenResult.AccessToken.Substring(0, Math.Min(15, tokenResult.AccessToken.Length))}...");
                                
                                // Add a small delay to allow token propagation
                                await Task.Delay(500);
                                
                                var userInfoResponse = await userInfoClient.GetAsync(UserInfoEndpoint);
                                var userInfoContent = await userInfoResponse.Content.ReadAsStringAsync();
                                
                                System.Diagnostics.Debug.WriteLine($"User info response status: {userInfoResponse.StatusCode}");
                                
                                if (userInfoResponse.IsSuccessStatusCode)
                                {
                                    UserInfoResponse? userInfo = await userInfoResponse.Content.ReadFromJsonAsync<UserInfoResponse>();
                                    if (userInfo == null)
                                    {
                                        throw new Exception("User info response was null");
                                    }
                                    System.Diagnostics.Debug.WriteLine($"User authenticated: {userInfo.Email} ({userInfo.Name})");

                                    return new AuthResponse
                                    {
                                        AuthSuccessful = true,
                                        OAuthToken = tokenResult.AccessToken,
                                        SessionId = Guid.Empty,
                                        NewAccount = false
                                    };
                                    
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"User info request failed: {userInfoContent}");
                                    
                                    // Try to extract basic info from ID token if available
                                    if (!string.IsNullOrEmpty(tokenResult.IdToken))
                                    {
                                        try
                                        {
                                            var basicUserInfo = ExtractUserInfoFromIdToken(tokenResult.IdToken);
                                            if (basicUserInfo != null)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Extracted user info from ID token: {basicUserInfo.Email} ({basicUserInfo.Name})");
                                            }
                                        }
                                        catch (Exception idEx)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Failed to extract info from ID token: {idEx.Message}");
                                        }
                                    }
                                    
                                    // If we couldn't get user info but have a valid token, still return success
                                    return new AuthResponse
                                    {
                                        AuthSuccessful = true,
                                        OAuthToken = tokenResult.AccessToken,
                                        SessionId = Guid.Empty,
                                        NewAccount = false
                                    };
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error getting user info: {ex.Message}");
                            
                            // If we couldn't get user info but have a valid token, still return success
                            return new AuthResponse
                            {
                                AuthSuccessful = true,
                                SessionId = Guid.Empty,
                                OAuthToken = tokenResult.AccessToken,
                                NewAccount = false
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing token response: {ex.Message}");
                        return new AuthResponse
                        {
                            AuthSuccessful = false,
                            OAuthToken = string.Empty,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }
                }
                else
                {
                    // Log the error response
                    System.Diagnostics.Debug.WriteLine($"Token request failed: {tokenResponse.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"Error details: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during token exchange: {ex.Message}");
            }
            
            return new AuthResponse
            {
                AuthSuccessful = false,
                OAuthToken = string.Empty,
                SessionId = Guid.Empty,
                NewAccount = false
            };
        }

        public async Task<AuthResponse> SignInWithGoogleAsync(Window parentWindow)
        {
            var tcs = new TaskCompletionSource<AuthResponse>();
            
            try
            {
                // Create and show the WebView dialog for Google sign-in
                ContentDialog webViewDialog = new ContentDialog
                {
                    Title = "Sign in with Google",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = parentWindow.Content.XamlRoot
                };

                var webView = new WebView2();
                webView.Width = 450;
                webView.Height = 600;
                webViewDialog.Content = webView;

                // Initialize WebView and navigate to auth URL
                await webView.EnsureCoreWebView2Async();
                
                bool authCodeFound = false;
                
                // Monitor document changes to automatically extract the authorization code
                webView.CoreWebView2.DOMContentLoaded += async (sender, args) =>
                {
                    try
                    {
                        // Get the current source URL
                        var currentUrl = webView.CoreWebView2.Source;
                        
                        // Check if this is the success page with the auth code
                        var title = await webView.CoreWebView2.ExecuteScriptAsync("document.title");
                        System.Diagnostics.Debug.WriteLine($"Page title: {title}");
                        
                        // For the OAuth approval page, we need a more specific approach
                        if (currentUrl.Contains("accounts.google.com/o/oauth2/approval"))
                        {
                            var codeBoxContent = await webView.CoreWebView2.ExecuteScriptAsync(@"
                                (function() {
                                    // Look for code specifically in Google's OAuth approval page
                                    const codeElement = document.querySelector('textarea.kHn9Lb');
                                    if (codeElement) return codeElement.textContent;
                                    
                                    // Try to find code in any element with a specific class or input
                                    const possibleCodeElements = document.querySelectorAll('code, pre, textarea, input[readonly]');
                                    for (const el of possibleCodeElements) {
                                        const content = el.textContent || el.value;
                                        if (content && content.length > 10) return content;
                                    }
                                    
                                    return '';
                                })()
                            ");
                            
                            // Clean up the result (remove quotes from JS)
                            var code = codeBoxContent.Trim('"');
                            if (!string.IsNullOrEmpty(code) && code != "null" && !authCodeFound)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found authorization code in OAuth approval page: {code.Substring(0, Math.Min(10, code.Length))}...");
                                authCodeFound = true;
                                
                                // Process the code
                                var response = await ExchangeCodeForTokenAsync(code);
                                System.Diagnostics.Debug.WriteLine($"Auth result: {response.AuthSuccessful}");
                                
                                // Close dialog and return
                                parentWindow.DispatcherQueue.TryEnqueue(() =>
                                {
                                    try { webViewDialog.Hide(); } catch { }
                                    tcs.SetResult(response);
                                });
                                
                                return; // Exit early once we've found the code
                            }
                        }
                        
                        // The auth code page has "Success code=" in the title, or the code might be in the page content
                        if (title.Contains("Success") || title.Contains("code"))
                        {
                            string code = string.Empty;
                            
                            // Try to get code from title
                            if (title.Contains("Success code="))
                            {
                                // Remove quotes that come from JS
                                title = title.Replace("\"", "");
                                code = title.Substring(title.IndexOf("Success code=") + "Success code=".Length);
                                System.Diagnostics.Debug.WriteLine($"Found code in title: {code.Substring(0, Math.Min(10, code.Length))}...");
                            }
                            
                            // Try to extract code from page if not in title
                            if (string.IsNullOrEmpty(code))
                            {
                                // Try to extract from the page content
                                var pageText = await webView.CoreWebView2.ExecuteScriptAsync(
                                    "document.body.innerText");
                                
                                // Look for patterns in the text (this may need adjustment based on actual output)
                                if (pageText.Contains("code="))
                                {
                                    int startIndex = pageText.IndexOf("code=") + 5;
                                    int endIndex = pageText.IndexOf("\"", startIndex);
                                    if (endIndex > startIndex)
                                    {
                                        code = pageText.Substring(startIndex, endIndex - startIndex);
                                        System.Diagnostics.Debug.WriteLine($"Found code in page: {code.Substring(0, Math.Min(10, code.Length))}...");
                                    }
                                }
                                
                                // Try to find code in a code element or input field
                                var codeElement = await webView.CoreWebView2.ExecuteScriptAsync(
                                    "document.querySelector('code') ? document.querySelector('code').innerText : ''");
                                if (!string.IsNullOrEmpty(codeElement) && codeElement != "\"\"")
                                {
                                    code = codeElement.Trim('"');
                                    System.Diagnostics.Debug.WriteLine($"Found code in code element: {code.Substring(0, Math.Min(10, code.Length))}...");
                                }
                            }
                            
                            // If we found a code, process it
                            if (!string.IsNullOrEmpty(code) && !authCodeFound)
                            {
                                authCodeFound = true;
                                System.Diagnostics.Debug.WriteLine($"Automatically extracted authorization code");
                                
                                // Process the authentication code
                                var response = await ExchangeCodeForTokenAsync(code);
                                System.Diagnostics.Debug.WriteLine($"Authentication result: {response.AuthSuccessful}, Token: {(string.IsNullOrEmpty(response.OAuthToken) ? "Empty" : "Present")}");
                                
                                // Close the dialog and return the result
                                parentWindow.DispatcherQueue.TryEnqueue(() =>
                                {
                                    try
                                    {
                                        webViewDialog.Hide();
                                    }
                                    catch { /* ignore errors if dialog is already closed */ }
                                    
                                    tcs.SetResult(response);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error extracting code: {ex.Message}");
                    }
                };

                // Also listen to navigation events to catch redirects
                webView.NavigationCompleted += async (sender, args) =>
                {
                    try
                    {
                        // Use the WebView's Source or CoreWebView2.Source to get the current URI
                        var uri = webView.Source?.ToString() ?? webView.CoreWebView2.Source;
                        System.Diagnostics.Debug.WriteLine($"Navigation completed to: {uri}");
                        
                        // For Google's OOB flow, check if we're on the approval page
                        if (uri.Contains("accounts.google.com/o/oauth2/approval") && !authCodeFound)
                        {
                            // Try to extract the code from the page
                            try
                            {
                                // Wait a moment for page to fully render
                                await Task.Delay(500);
                                
                                // Try several ways to extract the code
                                string code = string.Empty;
                                
                                // Try to get from page text that might contain "code="
                                var pageText = await webView.CoreWebView2.ExecuteScriptAsync(
                                    "document.body.innerText");
                                
                                if (pageText.Contains("code="))
                                {
                                    int startIndex = pageText.IndexOf("code=") + 5;
                                    int endIndex = pageText.IndexOf(" ", startIndex);
                                    if (endIndex > startIndex)
                                    {
                                        code = pageText.Substring(startIndex, endIndex - startIndex);
                                        code = code.Replace("\"", "").Trim();
                                        System.Diagnostics.Debug.WriteLine($"Extracted code from approval page text: {code.Substring(0, Math.Min(10, code.Length))}...");
                                    }
                                }
                                
                                // Try to find a code element, which often contains the auth code
                                var codeElements = await webView.CoreWebView2.ExecuteScriptAsync(
                                    "Array.from(document.querySelectorAll('code, .auth-code, input[readonly]')).map(el => el.innerText || el.value)");
                                
                                if (codeElements != "[]" && !string.IsNullOrEmpty(codeElements))
                                {
                                    // Parse the JS array result
                                    var elements = codeElements.Trim('[', ']').Split(',');
                                    foreach (var element in elements)
                                    {
                                        var value = element.Trim('"', ' ');
                                        if (!string.IsNullOrEmpty(value) && value.Length > 10)
                                        {
                                            code = value;
                                            System.Diagnostics.Debug.WriteLine($"Extracted code from element: {code.Substring(0, Math.Min(10, code.Length))}...");
                                            break;
                                        }
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(code) && !authCodeFound)
                                {
                                    authCodeFound = true;
                                    
                                    // Process the authentication code
                                    var response = await ExchangeCodeForTokenAsync(code);
                                    
                                    // Close the dialog and return the result
                                    parentWindow.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            webViewDialog.Hide();
                                        }
                                        catch { /* ignore errors if dialog is already closed */ }
                                        
                                        tcs.SetResult(response);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error extracting code from approval page: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation completed event error: {ex.Message}");
                    }
                };
                
                var authUrl = GetAuthorizationUrl();
                System.Diagnostics.Debug.WriteLine($"Navigating to OAuth URL: {authUrl}");
                webView.CoreWebView2.Navigate(authUrl);
                
                // Show the dialog
                var dialogResult = await webViewDialog.ShowAsync();
                
                // If user closed the dialog manually and we haven't processed a code yet
                if (!tcs.Task.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("Dialog closed manually before authentication completed");
                    tcs.SetResult(new AuthResponse
                    {
                        AuthSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Authentication error: {ex.Message}");
                tcs.TrySetException(ex);
            }
            
            return await tcs.Task;
        }

        private UserInfoResponse ExtractUserInfoFromIdToken(string idToken)
        {
            // This is a simple JWT parser that doesn't validate signatures
            var parts = idToken.Split('.');
            if (parts.Length != 3)
            {
                throw new ArgumentException("Invalid JWT format");
            }
            
            try
            {
                // Get the payload (second part)
                var payload = parts[1];
                
                // Add padding if needed
                while (payload.Length % 4 != 0)
                {
                    payload += '=';
                }
                
                // Decode the Base64Url encoded JSON
                var jsonBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
                var json = Encoding.UTF8.GetString(jsonBytes);
                
                // Parse the JSON
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var result = System.Text.Json.JsonSerializer.Deserialize<UserInfoResponse>(json, options);
                if (result == null) {
                    throw new Exception("Failed to deserialize user info from ID token");
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing ID token: {ex.Message}", ex);
            }
        }
    }

    internal class TokenResponse
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

    internal class UserInfoResponse
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