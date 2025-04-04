using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.OAuthProviders
{
    public class LinkedInOAuthHelper
    {
        private readonly string _clientId = "86j0ikb93jm78x";
        private readonly string _clientSecret = "WPL_AP1.pg2Bd1XhCi821VTG.+hatTA==";
        private readonly string _redirectUri = "http://localhost:8891/auth";
        private readonly string _scope = "openid profile email";
        private TaskCompletionSource<AuthResponse>? _tcs;
        private readonly UserAdapter userAdapter = new UserAdapter();

        public LinkedInOAuthHelper(string clientId, string clientSecret, string redirectUri, string scope)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _redirectUri = redirectUri;
            _scope = scope;
            LinkedInLocalOAuthServer.OnCodeReceived += OnCodeReceived;
        }

        private string BuildAuthorizeUrl()
        {
            var url = $"https://www.linkedin.com/oauth/v2/authorization" +
                      $"?response_type=code" +
                      $"&client_id={_clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                      $"&scope={Uri.EscapeDataString(_scope)}";
            Debug.WriteLine("Authorize URL: " + url);
            return url;
        }
        private async Task<(string, string)> GetLinkedInIdAndNameAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var response = await client.GetAsync("https://api.linkedin.com/v2/me");
                if (!response.IsSuccessStatusCode)
                    return (string.Empty, string.Empty);
                string json = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    string id = root.GetProperty("id").GetString() ?? throw new Exception("LinkedIn ID not found in response.");
                    string firstName = root.GetProperty("localizedFirstName").GetString() ?? throw new Exception("LinkedIn first name not found in response.");
                    string lastName = root.GetProperty("localizedLastName").GetString() ?? throw new Exception("LinkedIn last name not found in response.");
                    return (id, $"{firstName} {lastName}");
                }
            }
            throw new Exception("Failed to get LinkedIn ID and name.");
        }
        private async void OnCodeReceived(string code)
        {
            if (_tcs == null || _tcs.Task.IsCompleted) return;

            try
            {
                var token = await ExchangeCodeForToken(code);

                (string id, string name) = await GetLinkedInIdAndNameAsync(token);

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    Debug.WriteLine("LinkedIn ID or name is empty.");
                    _tcs.TrySetResult(new AuthResponse
                    {
                        AuthSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    });
                    return;
                }

                var user = userAdapter.GetUserByUsername(name);
                if (user == null)
                {
                    User newUser = new User
                    {
                        Username = name,
                        PasswordHash = string.Empty,
                        UserId = Guid.NewGuid(),
                        TwoFASecret = string.Empty,
                    };
                    userAdapter.CreateUser(newUser);
                    _tcs.TrySetResult(new AuthResponse
                    {
                        AuthSuccessful = true,
                        OAuthToken = token,
                        SessionId = newUser.UserId,
                        NewAccount = true
                    });
                }
                else
                {
                    _tcs.TrySetResult(new AuthResponse
                    {
                        AuthSuccessful = true,
                        OAuthToken = token,
                        SessionId = user.UserId,
                        NewAccount = false
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LinkedIn token exchange error: " + ex);
                _tcs.TrySetResult(new AuthResponse
                {
                    AuthSuccessful = false,
                    OAuthToken = string.Empty,
                    SessionId = Guid.Empty,
                    NewAccount = false
                });
            }
        }

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

        private async Task<string> ExchangeCodeForToken(string code)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.linkedin.com/oauth/v2/accessToken");
                request.Headers.Add("Accept", "application/json");
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret)
                });
                request.Content = content;

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
                {
                    return tokenProp.GetString() ?? throw new Exception("Token is null");
                }
                throw new Exception("Token not found in response");
            }
        }
    }
}
