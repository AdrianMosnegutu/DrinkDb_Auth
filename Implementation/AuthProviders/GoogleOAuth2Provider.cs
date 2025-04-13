using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Security.Cryptography;
using DrinkDb_Auth.Adapter;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GoogleOAuth2Provider : GenericOAuth2Provider
    {
        public static Guid CreateGloballyUniqueIdentifier(string identifier)
        {
            using (MD5 cryptographicHasher = MD5.Create())
            {
                byte[] hashResult = cryptographicHasher.ComputeHash(Encoding.UTF8.GetBytes(identifier));
                return new Guid(hashResult);
            }
        }

        private string ClientId { get; }
        private string ClientSecret { get; }

        private string RedirectUniformResourceIdentifier { get; }
        private string AuthorizationEndpoint { get; }
        private string TokenEndpoint { get; }
        private string UserInformationEndpoint { get; }

        private readonly string[] userResourcesScope = { "profile", "email" };
        private HttpClient httpClient;
        private static readonly ISessionAdapter SessionDatabaseAdapter = new SessionAdapter();
        private static readonly IUserAdapter UserDatabaseAdapter = new UserAdapter();

        private const string APP_CONFIG_CLIENT_ID_LABEL = "GoogleClientId";
        private const string APP_CONFIG_CLIENT_SECRET_LABEL = "GoogleClientSecret";
        private const string APP_CONFIG_REDIRECT_URI_LABEL = "GoogleRedirectUniformResourceIdentifier";
        private const string APP_CONFIG_AUTORIZATION_ENDPOINT_LABEL = "GoogleAuthorizationEndpoint";
        private const string APP_CONFIG_TOKEN_ENDPOINT_LABEL = "GoogleTokenEndpoint";
        private const string APP_CONFIG_USER_INFO_ENDPOINT_LABEL = "GoogleUserInfoEndpoint";

        private Guid EnsureUserExists(string identifier, string email, string name)
        {
            Guid userId = GoogleOAuth2Provider.CreateGloballyUniqueIdentifier(identifier);
            User? user = GoogleOAuth2Provider.UserDatabaseAdapter.GetUserById(userId);

            switch (user)
            {
                case null:
                    // Don't know why email is used as username but let's vibe with it
                    User newUser = new User { UserId = userId, Username = email, PasswordHash = string.Empty, TwoFASecret = null };
                    bool wasCreated = GoogleOAuth2Provider.UserDatabaseAdapter.CreateUser(newUser);
                    break;
                case not null:
                    break;
            }

            return userId;
        }

        public GoogleOAuth2Provider()
        {
            System.Collections.Specialized.NameValueCollection appSettings = System.Configuration.ConfigurationManager.AppSettings;
            this.httpClient = new HttpClient();
            string notFoundMessage = "not found";

            this.ClientId = appSettings[GoogleOAuth2Provider.APP_CONFIG_CLIENT_ID_LABEL] ?? notFoundMessage;
            this.ClientSecret = appSettings[GoogleOAuth2Provider.APP_CONFIG_CLIENT_SECRET_LABEL] ?? notFoundMessage;
            this.RedirectUniformResourceIdentifier = appSettings[GoogleOAuth2Provider.APP_CONFIG_REDIRECT_URI_LABEL] ?? notFoundMessage;
            this.AuthorizationEndpoint = appSettings[GoogleOAuth2Provider.APP_CONFIG_AUTORIZATION_ENDPOINT_LABEL] ?? notFoundMessage;
            this.TokenEndpoint = appSettings[GoogleOAuth2Provider.APP_CONFIG_TOKEN_ENDPOINT_LABEL] ?? notFoundMessage;
            this.UserInformationEndpoint = appSettings[GoogleOAuth2Provider.APP_CONFIG_USER_INFO_ENDPOINT_LABEL] ?? notFoundMessage;
        }

        public AuthenticationResponse Authenticate(string userId, string token)
        {
<<<<<<< Updated upstream
            return new AuthenticationResponse { IsAuthenticationSuccessful = !string.IsNullOrEmpty(token), OAuthenticationToken = token, SessionId = Guid.Empty, NewAccount = false };
=======
            // This method is used for validating an existing token, but we'll focus on the initial auth flow
            var response = new AuthResponse
            {
                IsAuthenticationSuccessful = !string.IsNullOrEmpty(token),
                OAuthToken = token,
                SessionId = Guid.Empty,
                NewAccount = false
            };

            return response;
>>>>>>> Stashed changes
        }

        public string GetAuthorizationUrl()
        {
            string allowedResourcesScope = string.Join(" ", userResourcesScope);

            Dictionary<string, string> authorizationData = new Dictionary<string, string>
            {
                { "client_id", this.ClientId },
                { "redirect_uri", this.RedirectUniformResourceIdentifier },
                { "response_type", "code" },
                { "scope", allowedResourcesScope },
                { "access_type", "offline" },
                { "state", Guid.NewGuid().ToString() }
            };

            string transformedURLData = string.Join("&", authorizationData.Select(row => $"{Uri.EscapeDataString(row.Key)}={Uri.EscapeDataString(row.Value)}"));
            string fullAuthorizationURL = $"{AuthorizationEndpoint}?{transformedURLData}";

            return fullAuthorizationURL;
        }

        public async Task<AuthenticationResponse> ExchangeCodeForTokenAsync(string code)
        {
            Dictionary<string, string> tokenRequest = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "redirect_uri", RedirectUniformResourceIdentifier },
                { "grant_type", "authorization_code" }
            };

            // Whoever wrote this many nested catches, I genuinelly hate you :<
            try
            {
                FormUrlEncodedContent formatContent = new FormUrlEncodedContent(tokenRequest);
                HttpResponseMessage tokenResponse = await httpClient.PostAsync(TokenEndpoint, formatContent);
                string responseContent = await tokenResponse.Content.ReadAsStringAsync();

                switch (tokenResponse.IsSuccessStatusCode)
                {
                    case true:
                        TokenResponse? tokenResult = null;

                        try
                        {
                            tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
                        }
                        catch
                        {
                        }

                        System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                        try
                        {
                            tokenResult = tokenResult == null ? System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseContent, options) : tokenResult;
                        }
                        catch
                        {
                        }

                        if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
                        {
<<<<<<< Updated upstream
                            return new AuthenticationResponse { IsAuthenticationSuccessful = false, OAuthenticationToken = tokenResult?.AccessToken, SessionId = Guid.Empty, NewAccount = false };
=======
                            System.Diagnostics.Debug.WriteLine("ERROR: Token result was null or access token was empty");
                            System.Diagnostics.Debug.WriteLine($"Raw response content: {responseContent}");
                            return new AuthResponse
                            {
                                IsAuthenticationSuccessful = false,
                                OAuthToken = tokenResult.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
>>>>>>> Stashed changes
                        }

                        UserInfoResponse userInformation;
                        System.Guid userId;
                        try
                        {
                            using (HttpClient httpClient = new HttpClient())
                            {
                                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
                                await Task.Delay(500);
                                HttpResponseMessage? httpClientResponse = await httpClient.GetAsync(UserInformationEndpoint);
                                string httpClientResponseContent = await httpClientResponse.Content.ReadAsStringAsync();

<<<<<<< Updated upstream
                                switch (httpClientResponse.IsSuccessStatusCode)
                                {
                                    case true:
                                        UserInfoResponse? httpClientInformation = await httpClientResponse.Content.ReadFromJsonAsync<UserInfoResponse>();

                                        if (httpClientInformation == null)
                                        {
                                            throw new Exception("Couldn't get http client informatin");
                                        }

                                        userInformation = this.ExtractUserInfoFromIdToken(tokenResult.IdToken);
                                        userId = this.EnsureUserExists(userInformation.Identifier, httpClientInformation.Email, httpClientInformation.Name);
                                        return new AuthenticationResponse { IsAuthenticationSuccessful = true, OAuthenticationToken = tokenResult.AccessToken, SessionId = SessionDatabaseAdapter.CreateSession(userId).SessionId, NewAccount = false };
                                    case false:
                                        if (string.IsNullOrEmpty(tokenResult.IdToken))
                                        {
                                            return new AuthenticationResponse { IsAuthenticationSuccessful = true, OAuthenticationToken = tokenResult.AccessToken, SessionId = Guid.Empty, NewAccount = false };
                                        }
                                        else
                                        {
                                            throw new Exception("Trigger Catch | Repeated code to attempt a succesfull authentication");
                                        }
=======
                                    // Extract user info from token
                                    var tokenUserInfo = ExtractUserInfoFromIdToken(tokenResult.IdToken);
                                    
                                    // Ensure the user exists in the database
                                    var userId = EnsureUserExists(tokenUserInfo.Sub, userInfo.Email, userInfo.Name);
                                    
                                    return new AuthResponse
                                    {
                                        IsAuthenticationSuccessful = true,
                                        OAuthToken = tokenResult.AccessToken,
                                        SessionId = sessionAdapter.CreateSession(userId).sessionId,
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
                                                
                                                // Ensure the user exists in the database
                                                var userId = EnsureUserExists(basicUserInfo.Sub, basicUserInfo.Email, basicUserInfo.Name);
                                                
                                                // If we couldn't get user info but have a valid token, still return success
                                                return new AuthResponse
                                                {
                                                    IsAuthenticationSuccessful = true,
                                                    OAuthToken = tokenResult.AccessToken,
                                                    SessionId = sessionAdapter.CreateSession(userId).sessionId, 
                                                    NewAccount = false
                                                };
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
                                        IsAuthenticationSuccessful = true,
                                        OAuthToken = tokenResult.AccessToken,
                                        SessionId = Guid.Empty,
                                        NewAccount = false
                                    };
>>>>>>> Stashed changes
                                }
                            }
                        }
                        catch
                        {
<<<<<<< Updated upstream
                            userInformation = ExtractUserInfoFromIdToken(tokenResult.IdToken);
                            userId = this.EnsureUserExists(userInformation.Identifier, userInformation.Email, userInformation.Name);
                            return new AuthenticationResponse { IsAuthenticationSuccessful = true, OAuthenticationToken = tokenResult.AccessToken, SessionId = SessionDatabaseAdapter.CreateSession(userId).SessionId, NewAccount = false };
                        }
                    case false:
                        throw new Exception("Trigger Catch | Repeated code to attempt a failed authentication");
=======
                            System.Diagnostics.Debug.WriteLine($"Error getting user info: {ex.Message}");

                            // Try to extract basic info from ID token
                            try 
                            {
                                var basicUserInfo = ExtractUserInfoFromIdToken(tokenResult.IdToken);
                                if (basicUserInfo != null)
                                {
                                    // Ensure the user exists in the database
                                    var userId = EnsureUserExists(basicUserInfo.Sub, basicUserInfo.Email, basicUserInfo.Name);
                                    
                                    // If we couldn't get user info but have a valid token, still return success
                                    return new AuthResponse
                                    {
                                        IsAuthenticationSuccessful = true,
                                        OAuthToken = tokenResult.AccessToken,
                                        SessionId = sessionAdapter.CreateSession(userId).sessionId,
                                        NewAccount = false
                                    };
                                }
                            }
                            catch (Exception idEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to extract info from ID token: {idEx.Message}");
                            }
                            
                            // Unable to extract user info, can't create user
                            return new AuthResponse
                            {
                                IsAuthenticationSuccessful = false,
                                OAuthToken = string.Empty,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing token response: {ex.Message}");
                        return new AuthResponse
                        {
                            IsAuthenticationSuccessful = false,
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
>>>>>>> Stashed changes
                }
            }
            catch
            {
                return new AuthenticationResponse { IsAuthenticationSuccessful = false, OAuthenticationToken = string.Empty, SessionId = Guid.Empty, NewAccount = false };
            }
<<<<<<< Updated upstream
=======
            
            return new AuthResponse
            {
                IsAuthenticationSuccessful = false,
                OAuthToken = string.Empty,
                SessionId = Guid.Empty,
                NewAccount = false
            };
>>>>>>> Stashed changes
        }

        public async Task<AuthenticationResponse> SignInWithGoogleAsync(Window parentWindow)
        {
            TaskCompletionSource<AuthenticationResponse> taskResults = new TaskCompletionSource<AuthenticationResponse>();
            try
            {
                ContentDialog googleSubWindow = new ContentDialog
                {
                    Title = "Sign in with Google",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = parentWindow.Content.XamlRoot
                };

                WebView2 webView = new WebView2();
                webView.Width = 450;
                webView.Height = 600;
                googleSubWindow.Content = webView;

                await webView.EnsureCoreWebView2Async();
                bool authenticationCodeFound = false;
                string approvalPath = "accounts.google.com/o/oauth2/approval";
                string domTitle = "document.title";
                string domBodyText = "document.body.innerText";
                string javaScriptQuery = "document.querySelector('code') ? document.querySelector('code').innerText : ''";
                string pageSuccesContent = "Succes", pageCodeContent = "code", pageSuccesCode = "Success code=";
                string pageCodeContentWithEqualAtTheEnd = pageCodeContent + "=";
                webView.CoreWebView2.DOMContentLoaded += async (sender, args) =>
                {
                    try
                    {
<<<<<<< Updated upstream
                        string currentUrl = webView.CoreWebView2.Source;
                        string title = await webView.CoreWebView2.ExecuteScriptAsync(domTitle);
                        string javaScriptCode = @"(function() 
                                                {
                                                    const codeElement = document.querySelector('textarea.kHn9Lb');
=======
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
                                System.Diagnostics.Debug.WriteLine($"Auth result: {response.IsAuthenticationSuccessful}");
                                
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
                                System.Diagnostics.Debug.WriteLine($"Authentication result: {response.IsAuthenticationSuccessful}, Token: {(string.IsNullOrEmpty(response.OAuthToken) ? "Empty" : "Present")}");
                                
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
>>>>>>> Stashed changes

                                                    if (codeElement) return codeElement.textContent;
                                    
                                                    const possibleCodeElements = document.querySelectorAll('code, pre, textarea, input[readonly]');

                                                    for (const el of possibleCodeElements)
                                                    {
                                                        const content = el.textContent || el.value;
                                                        if (content && content.length > 10) return content;
                                                    }
                                    
                                                    return '';
                                                })()";

                        switch (currentUrl.Contains(approvalPath))
                        {
                            case true:
                                string functionResult = await webView.CoreWebView2.ExecuteScriptAsync(javaScriptCode);
                                string trimmedResult = functionResult.Trim('"');

                                if (!string.IsNullOrEmpty(trimmedResult) && trimmedResult != "null" && !authenticationCodeFound)
                                {
                                    authenticationCodeFound = true;
                                    AuthenticationResponse authenticationResponse = await ExchangeCodeForTokenAsync(trimmedResult);
                                    parentWindow.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            googleSubWindow.Hide();
                                        }
                                        catch
                                        {
                                        }
                                        taskResults.SetResult(authenticationResponse);
                                    });

                                    return;
                                }
                                break;
                            case false:
                                break;
                        }

                        string code = string.Empty;
                        if (title.Contains(pageSuccesContent) || title.Contains(pageCodeContent))
                        {
                            switch (title.Contains(pageSuccesCode))
                            {
                                case true:
                                    title = title.Replace("\"", string.Empty);
                                    code = title.Substring(title.IndexOf("Success code=") + "Success code=".Length);
                                    break;
                            }

                            switch (string.IsNullOrEmpty(code))
                            {
                                case true:
                                    string pageContent = await webView.CoreWebView2.ExecuteScriptAsync(domBodyText);
                                    if (pageContent.Contains(pageCodeContentWithEqualAtTheEnd))
                                    {
                                        int skipNumberOfElements = 5;
                                        int startIndex = pageContent.IndexOf(pageCodeContentWithEqualAtTheEnd) + skipNumberOfElements;
                                        int endIndex = pageContent.IndexOf("\"", startIndex);

                                        code = endIndex > startIndex ? code = pageContent.Substring(startIndex, endIndex - startIndex) : code;
                                    }

                                    string codeElement = await webView.CoreWebView2.ExecuteScriptAsync(javaScriptQuery);

                                    code = !string.IsNullOrEmpty(codeElement) && codeElement != "\"\"" ? codeElement.Trim('"') : code;
                                    break;
                            }

                            switch (!string.IsNullOrEmpty(code) && !authenticationCodeFound)
                            {
                                case true:
                                    authenticationCodeFound = true;
                                    AuthenticationResponse response = await ExchangeCodeForTokenAsync(code);
                                    parentWindow.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            googleSubWindow.Hide();
                                        }
                                        catch
                                        {
                                        }
                                        taskResults.SetResult(response);
                                    });
                                    break;
                            }
                        }
                    }
                    catch
                    {
                    }
                };

                webView.NavigationCompleted += async (sender, args) =>
                {
                    try
                    {
<<<<<<< Updated upstream
                        string uniformResourceIdentifier = webView.Source?.ToString() ?? webView.CoreWebView2.Source;
                        if (uniformResourceIdentifier.Contains(approvalPath) && !authenticationCodeFound)
                        {
                            await Task.Delay(500);

                            string code = string.Empty;

                            string pageContent = await webView.CoreWebView2.ExecuteScriptAsync(domBodyText);

                            switch (pageContent.Contains(pageCodeContentWithEqualAtTheEnd))
                            {
                                case true:
                                    int skipNumberOfElements = 5;
                                    int startIndex = pageContent.IndexOf(pageCodeContentWithEqualAtTheEnd) + skipNumberOfElements;
                                    int endIndex = pageContent.IndexOf(" ", startIndex);

                                    code = endIndex > startIndex ? code = pageContent.Substring(startIndex, endIndex - startIndex).Replace("\"", string.Empty).Trim() : code;
                                    break;
                            }

                            string javaScriptArrayStream = "Array.from(document.querySelectorAll('code, .auth-code, input[readonly]')).map(el => el.innerText || el.value)";
                            string codeElements = await webView.CoreWebView2.ExecuteScriptAsync(javaScriptArrayStream);

                            switch (codeElements != "[]" && !string.IsNullOrEmpty(codeElements))
                            {
                                case true:
                                    string[] elements = codeElements.Trim('[', ']').Split(',');
                                    foreach (string element in elements)
                                    {
                                        string trimmedValue = element.Trim('"', ' ');
                                        if (!string.IsNullOrEmpty(trimmedValue) && trimmedValue.Length > 10)
                                        {
                                            code = trimmedValue;
                                            break;
                                        }
                                    }
                                    break;
                            }

                            switch (!string.IsNullOrEmpty(code) && !authenticationCodeFound)
                            {
                                case true:
                                    authenticationCodeFound = true;
                                    AuthenticationResponse response = await ExchangeCodeForTokenAsync(code);

                                    parentWindow.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            googleSubWindow.Hide();
                                        }
                                        catch
                                        {
                                        }
                                        taskResults.SetResult(response);
                                    });
                                    break;
                            }
                            }
                        }
                    catch
                    {
                    }
                };

                string authorizationURL = GetAuthorizationUrl();
                webView.CoreWebView2.Navigate(authorizationURL);
                ContentDialogResult subWindowResults = await googleSubWindow.ShowAsync();

                if (!taskResults.Task.IsCompleted)
                {
                    taskResults.SetResult(new AuthenticationResponse { AuthenticationSuccesfull = false, OAuthenticationToken = string.Empty, SessionId = Guid.Empty, NewAccount = false });
=======
                        IsAuthenticationSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    });
>>>>>>> Stashed changes
                }
            }
            catch (Exception ex)
            {
                taskResults.TrySetException(ex);
            }

            return await taskResults.Task;
        }

        private UserInfoResponse ExtractUserInfoFromIdToken(string idToken)
        {
            // Too many random numbers and chars to even pretend I know what's happening
            string[] splittedToken = idToken.Split('.');
            if (splittedToken.Length != 3)
            {
                throw new ArgumentException("Invalid JWT format");
            }

            try
            {
                int payloadIndex = 1;
                string payload = splittedToken[payloadIndex];

                while (payload.Length % 4 != 0)
                {
                    payload += '=';
                }

                byte[] jsonInBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
                string json = Encoding.UTF8.GetString(jsonInBytes);

                System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                UserInfoResponse? result = System.Text.Json.JsonSerializer.Deserialize<UserInfoResponse>(json, options);
                return result != null ? result : throw new Exception("Failed to deserialize user info from ID token");
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
        public required string Identifier { get; set; }

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
    }
}