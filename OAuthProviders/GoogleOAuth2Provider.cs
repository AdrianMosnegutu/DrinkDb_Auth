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

namespace DrinkDb_Auth.OAuthProviders
{
    public class GoogleOAuth2Provider : GenericOAuth2Provider
    {
        // These values should be loaded from configuration, not hard-coded
        private string ClientId { get; }
        private string ClientSecret { get; }
        private const string RedirectUri = "http://127.0.0.1:8080";
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
        }

        public AuthResponse Authenticate(string userId, string token)
        {
            // This method is used for validating an existing token, but we'll focus on the initial auth flow
            var response = new AuthResponse
            {
                AuthSuccessful = !string.IsNullOrEmpty(token),
                SessionToken = token,
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
            return $"{AuthorizationEndpoint}?{queryString}";
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

            var tokenResponse = await _httpClient.PostAsync(
                TokenEndpoint,
                new FormUrlEncodedContent(tokenRequestParameters)
            );

            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
                
                // Get user info with the access token
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
                
                var userInfoResponse = await _httpClient.GetAsync(UserInfoEndpoint);
                if (userInfoResponse.IsSuccessStatusCode)
                {
                    var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<UserInfoResponse>();
                    
                    // In a real implementation, you would check if the user exists in your database
                    // and create a new account if needed
                    var isNewAccount = false; // This would be determined by database lookup
                    
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        SessionToken = tokenResult.AccessToken,
                        NewAccount = isNewAccount
                    };
                }
            }
            
            return new AuthResponse
            {
                AuthSuccessful = false,
                SessionToken = string.Empty,
                NewAccount = false
            };
        }

        public async Task<AuthResponse> SignInWithGoogleAsync(Window parentWindow)
        {
            // Create a dialog to host the WebView2 control
            ContentDialog dialog = new ContentDialog
            {
                Title = "Sign in with Google",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = parentWindow.Content.XamlRoot
            };

            var webView = new WebView2();
            webView.Width = 450;
            webView.Height = 600;
            
            var tcs = new TaskCompletionSource<AuthResponse>();
            var httpListener = new HttpListener();
            
            try
            {
                // Start HTTP listener for the redirect
                httpListener.Prefixes.Add(RedirectUri + "/");
                httpListener.Start();
                
                // Start listening for the redirect in a separate task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait for the redirect request
                        var context = await httpListener.GetContextAsync();
                        var request = context.Request;
                        
                        // Parse the query string
                        var code = request.QueryString["code"];
                        
                        // Send a response to close the browser
                        using (var response = context.Response)
                        {
                            string responseString = "<html><head><title>Authentication Completed</title></head>" +
                                "<body>Authentication completed. You can close this window and return to the application.</body></html>";
                            var buffer = Encoding.UTF8.GetBytes(responseString);
                            response.ContentLength64 = buffer.Length;
                            response.ContentType = "text/html";
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        }
                        
                        // Process the authentication code
                        if (!string.IsNullOrEmpty(code))
                        {
                            var response = await ExchangeCodeForTokenAsync(code);
                            
                            // Use the dispatcher to close the dialog from the UI thread
                            parentWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                dialog.Hide();
                                tcs.SetResult(response);
                            });
                        }
                        else
                        {
                            parentWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                dialog.Hide();
                                tcs.SetResult(new AuthResponse
                                {
                                    AuthSuccessful = false,
                                    SessionToken = string.Empty,
                                    NewAccount = false
                                });
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        parentWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            dialog.Hide();
                            tcs.SetException(ex);
                        });
                    }
                    finally
                    {
                        httpListener.Stop();
                    }
                });
                
                // Still monitor WebView navigation to handle any special cases
                webView.NavigationStarting += (sender, args) =>
                {
                    var uri = new Uri(args.Uri);
                    // Log navigation for debugging
                    System.Diagnostics.Debug.WriteLine($"Navigating to: {uri.AbsoluteUri}");
                };

                dialog.Content = webView;
                
                // Initialize the WebView2
                await webView.EnsureCoreWebView2Async();
                
                // Navigate to the Google authorization URL
                var authUrl = GetAuthorizationUrl();
                webView.CoreWebView2.Navigate(authUrl);
                
                // Show the dialog and wait for the result
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                if (httpListener.IsListening)
                {
                    httpListener.Stop();
                }
            }
            
            return await tcs.Task;
        }
    }

    internal class TokenResponse
    {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; }
        public string IdToken { get; set; }
    }

    internal class UserInfoResponse
    {
        public string Sub { get; set; }
        public string Name { get; set; }
        public string GivenName { get; set; }
        public string FamilyName { get; set; }
        public string Picture { get; set; }
        public string Email { get; set; }
        public bool EmailVerified { get; set; }
        public string Locale { get; set; }
    }
} 