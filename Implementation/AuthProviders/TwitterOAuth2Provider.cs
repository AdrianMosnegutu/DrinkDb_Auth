using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;

namespace DrinkDb_Auth.OAuthProviders
{
    /// <summary>
    /// A PKCE-based OAuth 2.0 flow for Twitter in a WinUI desktop app.
    /// </summary>
    public class TwitterOAuth2Provider : GenericOAuth2Provider
    {
        // ──────── Constants ────────
        // The same Callback/Redirect URI you registered in Twitter Developer Portal.
        // e.g. "http://127.0.0.1:5000/x-callback"
        private const string RedirectUri = "http://127.0.0.1:5000/x-callback";

        // Twitter endpoints:
        private const string AuthorizationEndpoint = "https://twitter.com/i/oauth2/authorize";
        private const string TokenEndpoint = "https://api.twitter.com/2/oauth2/token";
        private const string UserInfoEndpoint = "https://api.twitter.com/2/users/me";

        // ──────── Configuration Fields ────────

        // ▼▼▼ 1) Set these appropriately ▼▼▼
        // Note: For native app flows, a client secret is often unused.
        private string ClientId { get; }
        private string ClientSecret { get; } // Usually not used for native app (PKCE) flows

        // ──────── OAuth State ────────

        // PKCE details: A code verifier is generated before sending the user to authorize.
        private string codeVerifier = string.Empty;

        // Scopes required for Twitter user details. "offline.access" is needed for refresh tokens.
        private readonly string[] scopes = { "tweet.read", "users.read" };

        // ──────── Dependencies ────────
        private readonly HttpClient httpClient;
        private static readonly SessionAdapter SessionAdapterInstance = new ();
        private static readonly UserAdapter UserAdapterInstance = new ();

        /// <summary>
        /// Converts the "sub" (subject) from Twitter into a GUID by hashing with MD5.
        /// This ensures a unique and consistent ID for users in our system.
        /// </summary>
        public static Guid ConvertSubToGuid(string twitterUserId)
        {
            using (var md5HashAlgorithm = MD5.Create()) // // Generates a 128-bit hashedBytes (same size as a Guid)
            {
                byte[] hashedBytes = md5HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(twitterUserId));
                return new Guid(hashedBytes);
            }
        }

        /// <summary>
        /// Checks if a user exists in the DB, and if not, creates a new one.
        /// Returns the unique GUID for the user.
        /// </summary>
        private Guid EnsureUserExists(string twitterUserId, string email, string fullName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Ensuring user exists with twitterUserId: {twitterUserId}, userEmail: {email}, fullName: {fullName}");
                var userId = ConvertSubToGuid(twitterUserId);
                System.Diagnostics.Debug.WriteLine($"Generated userId: {userId}");
                var existingUser = UserAdapterInstance.GetUserById(userId);
                System.Diagnostics.Debug.WriteLine($"Existing user found: {existingUser != null}");
                if (existingUser == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Creating new user with ID {userId} for Twitter user {email} ({fullName})");

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
                    using (var databaseConnection = DrinkDbConnectionHelper.GetConnection())
                    {
                        string insertUserSql = "INSERT INTO Users (userId, userName, passwordHash, twoFASecret, roleId) VALUES (@userId, @username, @passwordHash, @twoFASecret, @roleId);";
                        using (var insertCommand = new SqlCommand(insertUserSql, databaseConnection))
                        {
                            insertCommand.Parameters.AddWithValue("@userId", newUser.UserId);
                            insertCommand.Parameters.AddWithValue("@username", newUser.Username);
                            insertCommand.Parameters.AddWithValue("@passwordHash", (object?)newUser.PasswordHash ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@twoFASecret", (object?)newUser.TwoFASecret ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@roleId", defaultRoleId);

                            bool insertSucceded = insertCommand.ExecuteNonQuery() > 0;
                            System.Diagnostics.Debug.WriteLine($"User creation result: {insertSucceded}");

                            if (!insertSucceded)
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
                using (var databaseConnection = DrinkDbConnectionHelper.GetConnection())
                {
                    // Check if at least one role exists.
                    string checkRoleSql = "SELECT TOP 1 roleId FROM Roles";
                    using (var checkRoleCommand = new SqlCommand(checkRoleSql, databaseConnection))
                    {
                        object result = checkRoleCommand.ExecuteScalar();
                        if (result != null)
                        {
                            Guid existingRoleId = (Guid)result;
                            System.Diagnostics.Debug.WriteLine($"Found existing role with ID: {existingRoleId}");
                            return existingRoleId;
                        }
                    }

                    // If no role exists, we need to create one along with a permission.
                    System.Diagnostics.Debug.WriteLine("No existing roles found, creating a new role with permission");

                    // Step 1: Create a permission first.
                    Guid newPermissionId = Guid.NewGuid();
                    string createPermissionSql = "INSERT INTO Permissions (permissionId, permissionName, resource, action) VALUES (@permissionId, @permissionName, @resource, @action)";
                    using (var createPermissionCommand = new SqlCommand(createPermissionSql, databaseConnection))
                    {
                        createPermissionCommand.Parameters.AddWithValue("@permissionId", newPermissionId);
                        createPermissionCommand.Parameters.AddWithValue("@permissionName", "Basic Access");
                        createPermissionCommand.Parameters.AddWithValue("@resource", "general");
                        createPermissionCommand.Parameters.AddWithValue("@action", "read");
                        createPermissionCommand.ExecuteNonQuery();
                    }

                    // Step 2: Create a role that references that permission.
                    Guid roleId = Guid.NewGuid();
                    string createRoleSql = "INSERT INTO Roles (roleId, roleName, permissionId) VALUES (@roleId, @roleName, @permissionId)";
                    using (var createRoleCommand = new SqlCommand(createRoleSql, databaseConnection))
                    {
                        createRoleCommand.Parameters.AddWithValue("@roleId", roleId);
                        createRoleCommand.Parameters.AddWithValue("@roleName", "User");
                        createRoleCommand.Parameters.AddWithValue("@permissionId", newPermissionId);
                        createRoleCommand.ExecuteNonQuery();
                    }

                    // Step 3: Create a role-permission mapping (RolePermissions table).
                    string linkRolePermissionSql = "INSERT INTO RolePermissions (roleId, permissionId) VALUES (@roleId, @permissionId)";
                    using (var linkCommand = new SqlCommand(linkRolePermissionSql, databaseConnection))
                    {
                        linkCommand.Parameters.AddWithValue("@roleId", roleId);
                        linkCommand.Parameters.AddWithValue("@permissionId", newPermissionId);
                        linkCommand.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine($"Created new role with ID: {roleId}");
                    return roleId;
                }
            }
            catch (Exception ex)
            {
                // If we fail to create/find a role, return a new GUID instead of stopping the flow.
                System.Diagnostics.Debug.WriteLine($"Error in EnsureDefaultRoleExists: {ex.Message}");
                return Guid.NewGuid(); // Fallback
            }
        }

        /// <summary>
        /// Constructor reads the Client ID and Secret from config (if present)
        /// and stores them for use in the flow.
        /// </summary>
        public TwitterOAuth2Provider()
        {
            httpClient = new HttpClient();

            // Load from config (if you wish):
            ClientId = System.Configuration.ConfigurationManager.AppSettings["TwitterClientId"] ?? "YOUR_CLIENT_ID";
            ClientSecret = System.Configuration.ConfigurationManager.AppSettings["TwitterClientSecret"] ?? "YOUR_CLIENT_SECRET";

            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientId: {ClientId}");
            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientSecret: {ClientSecret.Substring(0, Math.Min(4, ClientSecret.Length))}... (not used in PKCE)");
        }

        /// <summary>
        /// Optional method to quickly verify a stored token (not used in this flow).
        /// </summary>
        public AuthenticationResponse Authenticate(string userId, string token)
        {
            return new AuthenticationResponse
            {
                AuthenticationSuccesfull = !string.IsNullOrEmpty(token),
                OAuthenticationToken = token,
                SessionId = Guid.Empty,
                NewAccount = false
            };
        }

        /// <summary>
        /// Generates the full authorization URL with PKCE code challenge and needed query params.
        /// </summary>
        public string GetAuthorizationUrl()
        {
            // Generate PKCE code verifier and code challenge.
            var (generatedCodeVerifier, generatedCodeChallenge) = GeneratePkceData();
            this.codeVerifier = generatedCodeVerifier;  // We'll need this later to exchange for a token.

            // Build up space-delimited scopes.
            var requestedScopes = string.Join(" ", scopes);
            var queryParameters = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "response_type", "code" },
                { "scope", requestedScopes },
                { "state", Guid.NewGuid().ToString() },

                // PKCE parameters
                { "code_challenge", generatedCodeChallenge },
                { "code_challenge_method", "S256" }
            };

            // Build the query string
            var queryString = string.Join("&", queryParameters
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

            var authorizationUrl = $"{AuthorizationEndpoint}?{queryString}";
            System.Diagnostics.Debug.WriteLine($"Generated authorization URL: {authorizationUrl}");
            return authorizationUrl;
        }

        /// <summary>
        /// When we get the code back from Twitter, exchange it for an access token.
        /// PKCE: We do NOT pass a client_secret, but we DO pass the same code_verifier we generated earlier.
        /// </summary>
        public async Task<AuthenticationResponse> ExchangeCodeForTokenAsync(string code)
        {
            // Prepare form data for token exchange.
            var tokenExchangeParameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" },
                { "code_verifier", codeVerifier }, // PKCE requirement
            };

            System.Diagnostics.Debug.WriteLine("Exchanging code for token (PKCE).");
            foreach (var tokenExchangeParameter in tokenExchangeParameters)
            {
                System.Diagnostics.Debug.WriteLine($"  {tokenExchangeParameter.Key}: {tokenExchangeParameter.Value}");
            }

            try
            {
                // Send the request to Twitter's token endpoint.
                using var content = new FormUrlEncodedContent(tokenExchangeParameters);
                var tokenResponse = await httpClient.PostAsync(TokenEndpoint, content);
                var responseContent = await tokenResponse.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"Token Response status: {tokenResponse.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Token Response content: {responseContent}");

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("Token request failed with non-success status.");
                    return new AuthenticationResponse
                    {
                        AuthenticationSuccesfull = false,
                        OAuthenticationToken = string.Empty,
                        SessionId = Guid.Empty,
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
                    return new AuthenticationResponse
                    {
                        AuthenticationSuccesfull = false,
                        OAuthenticationToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }

                // 4) Optionally, get user info
                try
                {
                    using var twitterUserInfoClient = new HttpClient();
                    twitterUserInfoClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
                    var userResp = await twitterUserInfoClient.GetAsync(UserInfoEndpoint);

                    System.Diagnostics.Debug.WriteLine($"Making request to Twitter user info endpoint: {UserInfoEndpoint}");

                    var userBody = await userResp.Content.ReadAsStringAsync();

                    if (!userResp.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"User info request failed. Response: {userBody}");
                        // We still have a valid token though
                        return new AuthenticationResponse
                        {
                            AuthenticationSuccesfull = false,
                            OAuthenticationToken = tokenResult.AccessToken,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }
                    try
                    {
                        // Parse the JSON response into our user model.
                        var twitterUserInfoObject = System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(userBody);
                        System.Diagnostics.Debug.WriteLine($"Authenticated user: {twitterUserInfoObject?.Data.Email} ({twitterUserInfoObject?.Data.Name})");

                        if (twitterUserInfoObject == null)
                        {
                            // If parsing user info fails, return a negative result.
                            System.Diagnostics.Debug.WriteLine("Failed to deserialize user info response");
                            return new AuthenticationResponse
                            {
                                AuthenticationSuccesfull = false,
                                OAuthenticationToken = tokenResult.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }

                        // Twitter might not always provide an email. We use the user's ID or fallback.
                        string twitterUserId = twitterUserInfoObject?.Data.Id ?? twitterUserInfoObject?.Data.Email ?? "unknown";
                        System.Diagnostics.Debug.WriteLine($"Using twitterUserId: {twitterUserId} for user creation");

                        try
                        {
                            // If Twitter doesn't return an email, we create a placeholder using the username.
                            string userEmail = twitterUserInfoObject?.Data.Email;
                            if (string.IsNullOrEmpty(userEmail))
                            {
                                // Fallback: build a fake email from username if needed.
                                userEmail = $"{twitterUserInfoObject?.Data.Username ?? "unknown"}@twitter.com";
                                System.Diagnostics.Debug.WriteLine($"No email provided by Twitter, using fallback: {userEmail}");
                            }

                            // Check or create the user in the local DB.
                            var applicationUserId = EnsureUserExists(twitterUserId, userEmail, twitterUserInfoObject?.Data.Name ?? "Unknown User");
                            System.Diagnostics.Debug.WriteLine($"User ID after EnsureUserExists: {applicationUserId}");

                            // Create a session for the user.
                            try
                            {
                                var sessionDetails = SessionAdapterInstance.CreateSession(applicationUserId);
                                System.Diagnostics.Debug.WriteLine($"Session created with ID: {sessionDetails.SessionId}");

                                // Return a success response with a valid session.
                                return new AuthenticationResponse
                                {
                                    AuthenticationSuccesfull = true,
                                    OAuthenticationToken = tokenResult.AccessToken,
                                    SessionId = sessionDetails.SessionId,
                                    NewAccount = false
                                };
                            }
                            catch (Exception sessionCreationException)
                            {
                                // If session creation fails, still inform the client with partial info.
                                System.Diagnostics.Debug.WriteLine($"Error creating session: {sessionCreationException.Message}");
                                return new AuthenticationResponse
                                {
                                    AuthenticationSuccesfull = false,
                                    OAuthenticationToken = tokenResult.AccessToken,
                                    SessionId = Guid.Empty,
                                    NewAccount = false
                                };
                            }
                        }
                        catch (Exception userCreationException)
                        {
                            // If user creation fails, return an unsuccessful response.
                            System.Diagnostics.Debug.WriteLine($"Error in EnsureUserExists: {userCreationException.Message}");
                            return new AuthenticationResponse
                            {
                                AuthenticationSuccesfull = false,
                                OAuthenticationToken = tokenResult.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }
                    }
                    catch (Exception userInfoDeserializationException)
                    {
                        // In case we cannot deserialize user info properly.
                        System.Diagnostics.Debug.WriteLine($"Error deserializing user info: {userInfoDeserializationException.Message}");
                        return new AuthenticationResponse
                        {
                            AuthenticationSuccesfull = false,
                            OAuthenticationToken = tokenResult.AccessToken,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }
                }
                catch (Exception userInfoFetchException)
                {
                    // If we can't fetch user info but got a valid token, let the user proceed with partial data.
                    System.Diagnostics.Debug.WriteLine($"Exception fetching user info: {userInfoFetchException.Message}");
                    return new AuthenticationResponse
                    {
                        AuthenticationSuccesfull = true,
                        OAuthenticationToken = tokenResult.AccessToken,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }
            }
            catch (Exception exceptionDuringTokenExchange)
            {
                // Catch any unexpected errors during token exchange.
                System.Diagnostics.Debug.WriteLine($"ExchangeCodeForTokenAsync exception: {exceptionDuringTokenExchange.Message}");
                return new AuthenticationResponse
                {
                    AuthenticationSuccesfull = false,
                    OAuthenticationToken = string.Empty,
                    SessionId = Guid.Empty,
                    NewAccount = false,
                };
            }
        }

        /// <summary>
        /// Shows a WebView, navigates to the Twitter OAuth page, intercepts the redirect to our local loopback.
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
        public async Task<AuthenticationResponse> SignInWithTwitterAsync(Window parentWindow)
        {
            var oauthFlowCompletionSource = new TaskCompletionSource<AuthenticationResponse>();

            try
            {
                var twitterLoginDialog = new ContentDialog
                {
                    Title = "Sign in with Twitter",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = parentWindow.Content.XamlRoot
                };

                // Setup the WebView to display the OAuth login page.
                var twitterOAuthWebView = new WebView2
                {
                    Width = 450,
                    Height = 600
                };
                twitterLoginDialog.Content = twitterOAuthWebView;

                // Ensure WebView2 is ready to navigate.
                await twitterOAuthWebView.EnsureCoreWebView2Async();

                // Listen for navigation events to detect when Twitter redirects back.
                twitterOAuthWebView.CoreWebView2.NavigationStarting += async (sender, navigationArgs) =>
                {
                    var navigatedUrl = navigationArgs.Uri;
                    System.Diagnostics.Debug.WriteLine($"NavigationStarting -> {navigatedUrl}");

                    // The redirect contains our authorization Code when it matches the redirect URI we set.
                    if (navigatedUrl.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase))
                    {
                        // Stop the WebView from continuing to this local URL.
                        navigationArgs.Cancel = true;

                        // Extract the  authorization Code from the URL.
                        var authorizationCode = ExtractQueryParameter(navigatedUrl, "code");
                        System.Diagnostics.Debug.WriteLine($"Found 'oauthResult]' in callback: {authorizationCode}");

                        // Exchange the authorization Code for an access token.
                        var oauthResult = await ExchangeCodeForTokenAsync(authorizationCode);

                        // Close the twitterLoginDialog and let the calling authorization Code handle the AuthenticationResult.
                        parentWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            twitterLoginDialog.Hide();
                            oauthFlowCompletionSource.SetResult(oauthResult);
                        });
                    }
                };

                // Start the authorization flow by navigating to Twitter's OAuth page.
                twitterOAuthWebView.CoreWebView2.Navigate(GetAuthorizationUrl());

                // Show the twitterLoginDialog to the user.
                await twitterLoginDialog.ShowAsync();

                // If the user closed the twitterLoginDialog manually, handle the case where we didn't get a authorization Code.
                if (!oauthFlowCompletionSource.Task.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("Dialog closed; no oauth code was returned.");
                    oauthFlowCompletionSource.SetResult(new AuthenticationResponse
                    {
                        AuthenticationSuccesfull = false,
                        OAuthenticationToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    });
                }
            }
            catch (Exception webViewError)
            {
                // Capture any critical errors in the process.
                System.Diagnostics.Debug.WriteLine($"SignInWithTwitterAsync critical failure: {webViewError.Message}");
                oauthFlowCompletionSource.TrySetException(webViewError);
            }

            // Return the result of this OAuth workflow.
            return await oauthFlowCompletionSource.Task;
        }

        /// <summary>
        /// Helper: parse one query param (e.g. ?code=xxx) from a URL
        /// </summary>
        private string ExtractQueryParameter(string fullUrl, string parameterName)
        {
            var parsedUri = new Uri(fullUrl);
            var rawQuery = parsedUri.Query.TrimStart('?');
            var queryPairs = rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in queryPairs)
            {
                var keyValuePairs = pair.Split('=', 2);
                if (keyValuePairs.Length == 2 && keyValuePairs[0] == parameterName)
                {
                    return Uri.UnescapeDataString(keyValuePairs[1]);
                }
            }
            throw new ArgumentException($"Parameter '{parameterName}' not found in URL: {fullUrl}", nameof(fullUrl));
        }

        /// <summary>
        /// Generate PKCE code_verifier (random) + code_challenge (SHA256).
        /// </summary>
        private (string codeVerifier, string codeChallenge) GeneratePkceData()
        {
            // Create a random array of bytes and then Base64Url-encode them to get a code_verifier.
            var secureRandom = RandomNumberGenerator.Create();
            var randomBytes = new byte[32];
            secureRandom.GetBytes(randomBytes);
            // Convert to a safe string for the OAuth request (no +, /, or =).
            var generatedCodeVerifier = Convert.ToBase64String(randomBytes)

                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Create a code_challenge by hashing the code_verifier with SHA256.
            using (var sha256Hasher = SHA256.Create())
            {
                var hashedVerifier = sha256Hasher.ComputeHash(Encoding.UTF8.GetBytes(generatedCodeVerifier));
                var generatedCodeChallenge = Convert.ToBase64String(hashedVerifier)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');

                return (generatedCodeVerifier, generatedCodeChallenge);
            }
        }

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

            var payloadSegment = parts[1];
            while (payloadSegment.Length % 4 != 0)
            {
                payloadSegment += '=';
            }

            var jsonBytes = Convert.FromBase64String(payloadSegment.Replace('-', '+').Replace('_', '/'));
            var jsonPayload = Encoding.UTF8.GetString(jsonBytes);

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(jsonPayload, jsonOptions)
                   ?? throw new Exception("Failed to deserialize ID token payload.");
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

        [JsonPropertyName("scope")]
         public required string Scope { get; set; }

        // Included only if requested and granted offline access.
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
        [JsonPropertyName("data")]
        public required TwitterUserData Data { get; set; }
    }

    internal class TwitterUserData
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("fullName")]

        public required string Name { get; set; }

        [JsonPropertyName("username")]
        public required string Username { get; set; }

        [JsonPropertyName("profile_image_url")]
        public required string ProfileImageUrl { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }
}
