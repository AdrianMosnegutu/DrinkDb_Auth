using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GitHubOAuthHelper
    {
        private const string ClientId = "Ov23ligheYgI7JILPWGY";  // Provided in your question
        private const string ClientSecret = "791dfaf36750b2a34a752c4fe3fb3703cef18836";
        private const string RedirectUri = "http://localhost:8890/auth";
        private const string Scope = "read:user user:email"; // Adjust scopes as needed

        private TaskCompletionSource<AuthResponse> _tcs;

        public GitHubOAuthHelper()
        {
            _tcs = new TaskCompletionSource<AuthResponse>();
            GitHubLocalOAuthServer.OnCodeReceived += OnCodeReceived;
        }

        /// <summary>
        /// Build the GitHub authorization URL using standard OAuth2 code flow.
        /// </summary>
        private string BuildAuthorizeUrl()
        {
            return $"https://github.com/login/oauth/authorize" +
                   $"?client_id={ClientId}" +
                   $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                   $"&scope={Uri.EscapeDataString(Scope)}" +
                   $"&response_type=code";
        }

        /// <summary>
        /// Called when the local server has received a GitHub code.
        /// We then exchange that code for an access token and set the result in _tcs.
        /// </summary>
        private async void OnCodeReceived(string code)
        {
            if (_tcs == null || _tcs.Task.IsCompleted)
                return;

            try
            {
                // Exchange code for an access token
                var token = await ExchangeCodeForToken(code);
                if (!string.IsNullOrEmpty(token))
                {
                    _tcs.TrySetResult(new AuthResponse
                    {
                        AuthSuccessful = true,
                        SessionToken = token,
                        NewAccount = false
                    });
                }
                else
                {
                    _tcs.TrySetResult(new AuthResponse
                    {
                        AuthSuccessful = false,
                        SessionToken = string.Empty,
                        NewAccount = false
                    });
                }
            }
            catch (Exception ex)
            {
                _tcs.TrySetResult(new AuthResponse
                {
                    AuthSuccessful = false,
                    SessionToken = string.Empty,
                    NewAccount = false
                });
                throw new Exception("Failed to exchange code for token.", ex);
            }
        }

        /// <summary>
        /// Actually open the user's default browser to the GitHub authorize page,
        /// then wait for the local server to get the code and do the exchange.
        /// </summary>
        public async Task<AuthResponse> AuthenticateAsync()
        {
            _tcs = new TaskCompletionSource<AuthResponse>();

            var authorizeUri = new Uri(BuildAuthorizeUrl());
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizeUri.ToString(),
                UseShellExecute = true
            });

            return await _tcs.Task;
        }

        /// <summary>
        /// POST to GitHub's /login/oauth/access_token to get an access token from the code.
        /// </summary>
        private async Task<string> ExchangeCodeForToken(string code)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
                request.Headers.Add("Accept", "application/json"); // we want JSON response
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("client_secret", ClientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", RedirectUri)
                });
                request.Content = content;

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                // GitHub returns JSON like: {"access_token":"...","token_type":"bearer","scope":"..."}
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
                {
                    return tokenProp.GetString() ?? throw new Exception("Access token is null.");
                }
                throw new Exception("Failed to get access token from GitHub.");
            }
        }
    }
}
