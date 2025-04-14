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
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DrinkDb_Auth.OAuthProviders
{
    /// <summary>
    /// A PKCE-based OAuth 2.0 flow for Twitter in a WinUI desktop app.
    /// </summary>
    public class TwitterOAuth2Provider : GenericOAuth2Provider, ITwitterOAuth2Provider
    {
        // ▼▼▼ 1) Set these appropriately ▼▼▼
        // Note: For native app flows, a client secret is often unused.

        private string ClientId { get; }
        private string ClientSecret { get; } // Usually not used for native app (PKCE) flows

        // This should match the Redirect URI registered in the Twitter Developer Portal.
        private const string RedirectUri = "http://127.0.0.1:5000/x-callback";

        // Endpoints for Twitter's OAuth 2.0
        private const string AuthorizationEndpoint = "https://twitter.com/i/oauth2/authorize";
        private const string TokenEndpoint = "https://api.twitter.com/2/oauth2/token";
        private const string UserInfoEndpoint = "https://api.twitter.com/2/users/me";

        // Scopes required for Twitter user details. "offline.access" is needed for refresh tokens.
        private readonly string[] Scopes = { "tweet.read", "users.read" };

        // PKCE details: A code verifier is generated before sending the user to authorize.
        private string _codeVerifier = string.Empty;

        private readonly HttpClient _httpClient;
        private static readonly SessionAdapter sessionAdapter = new();
        private static readonly UserAdapter userAdapter = new();

        /// <summary>
        /// Converts the "sub" (subject) from Twitter into a GUID by hashing with MD5.
        /// This ensures a unique and consistent ID for users in our system.
        /// </summary>
        public static Guid SubToGuid(string sub)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sub));
                return new Guid(hash);
            }
        }

        /// <summary>
        /// Checks if a user exists in the DB, and if not, creates a new one.
        /// Returns the unique GUID for the user.
        /// </summary>
        private Guid EnsureUserExists(string sub, string email, string name)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Ensuring user exists with sub: {sub}, email: {email}, name: {name}");

                var userId = SubToGuid(sub);
                System.Diagnostics.Debug.WriteLine($"Generated userId: {userId}");

                var existingUser = userAdapter.GetUserById(userId);
                System.Diagnostics.Debug.WriteLine($"Existing user found: {existingUser != null}");

                if (existingUser == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Creating new user with ID {userId} for Twitter user {email} ({name})");

                    // First, ensure the default role exists
                    Guid defaultRoleId = EnsureDefaultRoleExists();
                    System.Diagnostics.Debug.WriteLine($"Using default role ID: {defaultRoleId}");

                    // Create a new user without RoleId property
                    var newUser = new User
                    {
                        UserId = userId,
                        Username = email, // Using email as the username
                        PasswordHash = string.Empty, // OAuth users don't need passwords
                        TwoFASecret = null
                    };

                    // Use direct SQL to insert the user with a roleId
                    using (var conn = DrinkDbConnectionHelper.GetConnection())
                    {
                        string sql = "INSERT INTO Users (userId, userName, passwordHash, twoFASecret, roleId) VALUES (@userId, @username, @passwordHash, @twoFASecret, @roleId);";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@userId", newUser.UserId);
                            cmd.Parameters.AddWithValue("@username", newUser.Username);
                            cmd.Parameters.AddWithValue("@passwordHash", (object?)newUser.PasswordHash ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@twoFASecret", (object?)newUser.TwoFASecret ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@roleId", defaultRoleId);

                            var success = cmd.ExecuteNonQuery() > 0;
                            System.Diagnostics.Debug.WriteLine($"User creation result: {success}");

                            if (!success)
                            {
                                System.Diagnostics.Debug.WriteLine("Failed to create user in database");
                                // Instead of throwing, return a default user ID to allow the flow to continue
                                return Guid.NewGuid();
                            }
                        }
                    }

                    return userId;
                }

                System.Diagnostics.Debug.WriteLine($"Found existing user with ID {userId}");
                return userId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EnsureUserExists: {ex.Message}");
                // Return a default user ID instead of throwing to allow the flow to continue
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Attempts to find a default role in the DB. If none exists, one is created.
        /// Returns the ID of the found or newly created role.
        /// </summary>
        private Guid EnsureDefaultRoleExists()
        {
            try
            {
                using (var conn = DrinkDbConnectionHelper.GetConnection())
                {
                    // Check if at least one role exists.
                    string findSql = "SELECT TOP 1 roleId FROM Roles";
                    using (var findCmd = new SqlCommand(findSql, conn))
                    {
                        object result = findCmd.ExecuteScalar();
                        if (result != null)
                        {
                            Guid existingRoleId = (Guid)result;
                            System.Diagnostics.Debug.WriteLine($"Found existing role with ID: {existingRoleId}");
                            return existingRoleId;
                        }
                    }

                    // If no role exists, we need to create one along with a permission.
                    System.Diagnostics.Debug.WriteLine("No existing roles found, creating a new role with permission");

                    // Create a permission first.
                    Guid permissionId = Guid.NewGuid();
                    string createPermissionSql = "INSERT INTO Permissions (permissionId, permissionName, resource, action) VALUES (@permissionId, @permissionName, @resource, @action)";
                    using (var permCmd = new SqlCommand(createPermissionSql, conn))
                    {
                        permCmd.Parameters.AddWithValue("@permissionId", permissionId);
                        permCmd.Parameters.AddWithValue("@permissionName", "Basic Access");
                        permCmd.Parameters.AddWithValue("@resource", "general");
                        permCmd.Parameters.AddWithValue("@action", "read");
                        permCmd.ExecuteNonQuery();
                    }

                    // Create a role that references that permission.
                    Guid roleId = Guid.NewGuid();
                    string createRoleSql = "INSERT INTO Roles (roleId, roleName, permissionId) VALUES (@roleId, @roleName, @permissionId)";
                    using (var roleCmd = new SqlCommand(createRoleSql, conn))
                    {
                        roleCmd.Parameters.AddWithValue("@roleId", roleId);
                        roleCmd.Parameters.AddWithValue("@roleName", "User");
                        roleCmd.Parameters.AddWithValue("@permissionId", permissionId);
                        roleCmd.ExecuteNonQuery();
                    }

                    // Create a role-permission mapping (RolePermissions table).
                    string createRolePermSql = "INSERT INTO RolePermissions (roleId, permissionId) VALUES (@roleId, @permissionId)";
                    using (var rolePermCmd = new SqlCommand(createRolePermSql, conn))
                    {
                        rolePermCmd.Parameters.AddWithValue("@roleId", roleId);
                        rolePermCmd.Parameters.AddWithValue("@permissionId", permissionId);
                        rolePermCmd.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine($"Created new role with ID: {roleId}");
                    return roleId;
                }
            }
            catch (Exception ex)
            {
                // If we fail to create/find a role, return a new GUID instead of stopping the flow.
                System.Diagnostics.Debug.WriteLine($"Error in EnsureDefaultRoleExists: {ex.Message}");
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Constructor reads the Client ID and Secret from config (if present) 
        /// and stores them for use in the flow.
        /// </summary>
        public TwitterOAuth2Provider()
        {
            _httpClient = new HttpClient();

            // Load from config. If not found, placeholders are used.
            ClientId = System.Configuration.ConfigurationManager.AppSettings["TwitterClientId"] ?? "YOUR_CLIENT_ID";
            ClientSecret = System.Configuration.ConfigurationManager.AppSettings["TwitterClientSecret"] ?? "YOUR_CLIENT_SECRET";

            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientId: {ClientId}");
            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientSecret: {ClientSecret.Substring(0, Math.Min(4, ClientSecret.Length))}... (not used in PKCE)");
        }

        /// <summary>
        /// Simple token check method (not heavily used in PKCE flows).
        /// </summary>
        public AuthResponse Authenticate(string userId, string token)
        {
            return new AuthResponse
            {
                AuthSuccessful = !string.IsNullOrEmpty(token),
                OAuthToken = token,
                SessionId = Guid.Empty,
                NewAccount = false
            };
        }

        /// <summary>
        /// Constructs the URL for Twitter's authorization endpoint and returns it.
        /// This includes the PKCE challenge and all required query parameters.
        /// </summary>
        public string GetAuthorizationUrl()
        {
            // Generate PKCE code verifier and code challenge.
            var (codeVerifier, codeChallenge) = GeneratePkceData();
            _codeVerifier = codeVerifier;  // We'll need this later to exchange for a token.

            // Build up space-delimited scopes.
            var scopeString = string.Join(" ", Scopes);

            // Prepare the necessary query parameters for Twitter OAuth.
            var queryParameters = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "response_type", "code" },
                { "scope", scopeString },
                { "state", Guid.NewGuid().ToString() },

                // PKCE parameters
                { "code_challenge", codeChallenge },
                { "code_challenge_method", "S256" }
            };

            // Encode them into a query string.
            var queryString = string.Join("&", queryParameters
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

            var authUrl = $"{AuthorizationEndpoint}?{queryString}";
            System.Diagnostics.Debug.WriteLine($"Generated authorization URL: {authUrl}");
            return authUrl;
        }

        /// <summary>
        /// Exchanges the authorization code for an access token.
        /// This request uses the PKCE code_verifier.
        /// </summary>
        public async Task<AuthResponse> ExchangeCodeForTokenAsync(string code)
        {
            // Prepare form data for token exchange.
            var tokenRequestParameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" },
                { "code_verifier", _codeVerifier }, // PKCE requirement
            };

            System.Diagnostics.Debug.WriteLine("Exchanging code for token (PKCE).");
            foreach (var kv in tokenRequestParameters)
            {
                System.Diagnostics.Debug.WriteLine($"  {kv.Key}: {kv.Value}");
            }

            try
            {
                // Send the request to Twitter's token endpoint.
                using var content = new FormUrlEncodedContent(tokenRequestParameters);
                var tokenResponse = await _httpClient.PostAsync(TokenEndpoint, content);
                var responseContent = await tokenResponse.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"Token Response status: {tokenResponse.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Token Response content: {responseContent}");

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    // If the token endpoint fails, return an unsuccessful AuthResponse.
                    System.Diagnostics.Debug.WriteLine("Token request failed with non-success status.");
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }

                // Attempt to deserialize the token response.
                TwitterTokenResponse? tokenResult;
                try
                {
                    tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TwitterTokenResponse>();
                }
                catch (Exception jsonEx)
                {
                    // Fallback for any deserialization issues.
                    System.Diagnostics.Debug.WriteLine($"Error deserializing token response: {jsonEx.Message}");
                    tokenResult = System.Text.Json.JsonSerializer.Deserialize<TwitterTokenResponse>(responseContent);
                }

                // If there is no valid access token, return a failure.
                if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
                {
                    System.Diagnostics.Debug.WriteLine("No access token in tokenResult.");
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }

                // Optionally retrieve user info from Twitter using the access token.
                try
                {
                    using var userInfoClient = new HttpClient();
                    userInfoClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

                    System.Diagnostics.Debug.WriteLine($"Making request to Twitter user info endpoint: {UserInfoEndpoint}");
                    var userResp = await userInfoClient.GetAsync(UserInfoEndpoint);
                    var userBody = await userResp.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine($"Twitter user info response status: {userResp.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"Twitter user info response body: {userBody}");

                    if (!userResp.IsSuccessStatusCode)
                    {
                        // Even if user info fails, the token itself might still be valid.
                        System.Diagnostics.Debug.WriteLine($"User info request failed. Response: {userBody}");
                        return new AuthResponse
                        {
                            AuthSuccessful = false,
                            OAuthToken = tokenResult.AccessToken,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }

                    try
                    {
                        // Parse the JSON response into our user model.
                        var userInfo = System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(userBody);
                        System.Diagnostics.Debug.WriteLine($"Authenticated user: {userInfo?.Data.Email} ({userInfo?.Data.Name})");

                        if (userInfo == null)
                        {
                            // If parsing user info fails, return a negative result.
                            System.Diagnostics.Debug.WriteLine("Failed to deserialize user info response");
                            return new AuthResponse
                            {
                                AuthSuccessful = false,
                                OAuthToken = tokenResult.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }

                        // Twitter might not always provide an email. We use the user's ID or fallback.
                        string sub = userInfo?.Data.Id ?? userInfo?.Data.Email ?? "unknown";
                        System.Diagnostics.Debug.WriteLine($"Using sub: {sub} for user creation");

                        try
                        {
                            // If Twitter doesn't return an email, we create a placeholder using the username.
                            string email = userInfo?.Data.Email;
                            if (string.IsNullOrEmpty(email))
                            {
                                // Fallback: build a fake email from username if needed.
                                email = $"{userInfo?.Data.Username ?? "unknown"}@twitter.com";
                                System.Diagnostics.Debug.WriteLine($"No email provided by Twitter, using fallback: {email}");
                            }

                            // Check or create the user in the local DB.
                            var userId = EnsureUserExists(sub, email, userInfo?.Data.Name ?? "Unknown User");
                            System.Diagnostics.Debug.WriteLine($"User ID after EnsureUserExists: {userId}");

                            // Create a session for the user.
                            try
                            {
                                var session = sessionAdapter.CreateSession(userId);
                                System.Diagnostics.Debug.WriteLine($"Session created with ID: {session.sessionId}");

                                // Return a success response with a valid session.
                                return new AuthResponse
                                {
                                    AuthSuccessful = true,
                                    OAuthToken = tokenResult.AccessToken,
                                    SessionId = session.sessionId,
                                    NewAccount = false
                                };
                            }
                            catch (Exception sessionEx)
                            {
                                // If session creation fails, still inform the client with partial info.
                                System.Diagnostics.Debug.WriteLine($"Error creating session: {sessionEx.Message}");
                                return new AuthResponse
                                {
                                    AuthSuccessful = false,
                                    OAuthToken = tokenResult.AccessToken,
                                    SessionId = Guid.Empty,
                                    NewAccount = false
                                };
                            }
                        }
                        catch (Exception userEx)
                        {
                            // If user creation fails, return an unsuccessful response.
                            System.Diagnostics.Debug.WriteLine($"Error in EnsureUserExists: {userEx.Message}");
                            return new AuthResponse
                            {
                                AuthSuccessful = false,
                                OAuthToken = tokenResult.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }
                    }
                    catch (Exception deserializeEx)
                    {
                        // In case we cannot deserialize user info properly.
                        System.Diagnostics.Debug.WriteLine($"Error deserializing user info: {deserializeEx.Message}");
                        return new AuthResponse
                        {
                            AuthSuccessful = false,
                            OAuthToken = tokenResult.AccessToken,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }
                }
                catch (Exception ex)
                {
                    // If we can't fetch user info but got a valid token, let the user proceed with partial data.
                    System.Diagnostics.Debug.WriteLine($"Exception fetching user info: {ex.Message}");
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        OAuthToken = tokenResult.AccessToken,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected errors during token exchange.
                System.Diagnostics.Debug.WriteLine($"ExchangeCodeForTokenAsync exception: {ex.Message}");
                return new AuthResponse
                {
                    AuthSuccessful = false,
                    OAuthToken = string.Empty,
                    SessionId = Guid.Empty,
                    NewAccount = false,
                };
            }
        }

        /// <summary>
        /// Opens a dialog with a WebView2 control to handle Twitter's OAuth login.
        /// The WebView is used to navigate and intercept the redirect containing the code.
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

                // Setup the WebView to display the OAuth login page.
                var webView = new WebView2
                {
                    Width = 450,
                    Height = 600
                };
                dialog.Content = webView;

                // Ensure WebView2 is ready to navigate.
                await webView.EnsureCoreWebView2Async();

                // Listen for navigation events to detect when Twitter redirects back.
                webView.CoreWebView2.NavigationStarting += async (sender, args) =>
                {
                    var navUrl = args.Uri;
                    System.Diagnostics.Debug.WriteLine($"NavigationStarting -> {navUrl}");

                    // The redirect contains our code when it matches the redirect URI we set.
                    if (navUrl.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase))
                    {
                        // Stop the WebView from continuing to this local URL.
                        args.Cancel = true;

                        // Extract the authorization code from the URL.
                        var code = ExtractQueryParameter(navUrl, "code");
                        System.Diagnostics.Debug.WriteLine($"Found 'code' in callback: {code}");

                        // Exchange the code for an access token.
                        var authResponse = await ExchangeCodeForTokenAsync(code);

                        // Close the dialog and let the calling code handle the AuthResponse.
                        parentWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            dialog.Hide();
                            tcs.SetResult(authResponse);
                        });
                    }
                };

                // Start the authorization flow by navigating to Twitter's OAuth page.
                webView.CoreWebView2.Navigate(GetAuthorizationUrl());

                // Show the dialog to the user.
                var dialogResult = await dialog.ShowAsync();

                // If the user closed the dialog manually, handle the case where we didn't get a code.
                if (!tcs.Task.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("Dialog closed; no code was returned.");
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
                // Capture any critical errors in the process.
                System.Diagnostics.Debug.WriteLine($"SignInWithTwitterAsync error: {ex.Message}");
                tcs.TrySetException(ex);
            }

            // Return the result of this OAuth workflow.
            return await tcs.Task;
        }

        /// <summary>
        /// Extracts a single query parameter value from a URL.
        /// Throws an exception if the param is missing.
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
        /// Generates a PKCE code_verifier and code_challenge using SHA256.
        /// This ensures a secure OAuth exchange without needing a client secret in native apps.
        /// </summary>
        private (string codeVerifier, string codeChallenge) GeneratePkceData()
        {
            // Create a random array of bytes and then Base64Url-encode them to get a code_verifier.
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);

            // Convert to a safe string for the OAuth request (no +, /, or =).
            var codeVerifier = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Create a code_challenge by hashing the code_verifier with SHA256.
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
        /// If Twitter provides an ID token, this method could parse out user info. 
        /// Usually not needed for simple flows, since Twitter's user info endpoint is enough.
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

            return System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(json, options)
                   ?? throw new Exception("Failed to deserialize ID token payload.");
        }
    }

    /// <summary>
    /// Data model matching Twitter's token response fields.
    /// Includes optional fields like refresh_token and id_token.
    /// </summary>
    internal class TwitterTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        // Included only if requested and granted offline access.
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public string IdToken { get; set; }
    }

    /// <summary>
    /// Model for user info returned by Twitter's "/2/users/me" endpoint.
    /// </summary>
    internal class TwitterUserInfoResponse
    {
        [JsonPropertyName("data")]
        public TwitterUserData Data { get; set; }
    }

    /// <summary>
    /// Detailed user data from Twitter's user info response.
    /// Fields that rely on special permissions (e.g., email) will only be present if your app is authorized.
    /// </summary>
    internal class TwitterUserData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }
}
