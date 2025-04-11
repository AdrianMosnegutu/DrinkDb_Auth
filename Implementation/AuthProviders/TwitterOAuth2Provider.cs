using System;
using System.Data;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using Microsoft.Data.SqlClient;

namespace DrinkDb_Auth.OAuthProviders
{
    /// <summary>
    /// A PKCE-based OAuth 2.0 flow for Twitter in a WinUI desktop app.
    /// </summary>
    public class TwitterOAuth2Provider : GenericOAuth2Provider
    {
        // ──────── Constants ────────

        // This should match the Redirect URI registered in the Twitter Developer Portal.
        private const string RedirectUri = "http://127.0.0.1:5000/x-callback";

        // Endpoints for Twitter's OAuth 2.0
        private const string AuthorizationEndpoint = "https://twitter.com/i/oauth2/authorize";
        private const string TokenEndpoint = "https://api.twitter.com/2/oauth2/token";
        private const string UserInfoEndpoint = "https://api.twitter.com/2/users/me";

        // ──────── Configuration Fields ────────

        // ▼▼▼ 1) Set these appropriately ▼▼▼
        // Note: For native app flows, a client secret is often unused.
        private string ClientId { get; }
        private string ClientSecret { get; } // Usually not used for native app (PKCE) flows

        // ──────── OAuth State ────────

        // PKCE details: A authorizationCode] verifier is generated before sending the user to authorize.
        private string codeVerifier = string.Empty;

        // Scopes required for Twitter user details. "offline.access" is needed for refresh tokens.
        private readonly string[] scopes = { "tweet.read", "users.read" };

        // ──────── Dependencies ────────
        private readonly HttpClient httpClient;
        private static readonly SessionAdapter SessionAdapterInstance = new ();
        private static readonly UserAdapter UserAdapterInstance = new ();

        /// <summary>
        /// Converts the "Sub" (subject) or "twitterUserId" from Twitter into a GUID by hashing with MD5.
        /// This ensures a unique and consistent ID for users in our system.
        /// </summary>
        public static Guid ConvertSubToGuid(string twitterUserId)
        {
            using (var md5HashAlgorithm = MD5.Create()) // Generates a 128-bit hashedBytes (same size as a Guid)
            {
                byte[] hashedBytes = md5HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(twitterUserId));
                return new Guid(hashedBytes);
            }
        }

        /// <summary>
        /// Ensures a user with the given Twitter ID exists in the dataBAse,if not, creates a new user.
        /// Returns the corresponding unique GUID used as internal user identifier.
        /// </summary>
        private Guid EnsureUserExists(string twitterUserId, string email, string fullName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Ensuring user exists with twitterUserId: {twitterUserId}, userEmail: {email}, fullName: {fullName}");

                var generatedUserId = ConvertSubToGuid(twitterUserId);
                System.Diagnostics.Debug.WriteLine($"Generated generatedUserId: {generatedUserId}");

                var userFromDatabase = UserAdapterInstance.GetUserById(generatedUserId);
                System.Diagnostics.Debug.WriteLine($"Existing user found: {userFromDatabase != null}");
                if (userFromDatabase == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Creating new user with ID {generatedUserId} for Twitter user {email} ({fullName})");
                    // First, ensure the default role exists
                    Guid defaultRoleId = EnsureDefaultRoleExists();
                    System.Diagnostics.Debug.WriteLine($"Using default role ID: {defaultRoleId}");
                    // Create a new user without RoleId property
                    var newUser = new User
                    {
                        UserId = generatedUserId,
                        Username = email, // Using userEmail as the username
                        PasswordHash = string.Empty, // OAuth users don't need passwords
                        TwoFASecret = null
                    };

                    // Use direct SQL to insert the user with a newRoleId
                    using (var databaseConnection = DrinkDbConnectionHelper.GetConnection())
                    {
                      const string insertUserSql = "INSERT INTO Users (applicationUserId, userName, passwordHash, twoFASecret, roleId)" +
                                                  " VALUES (@applicationUserId, @username, @passwordHash, @twoFASecret, @roleId);";
                        using (var insertCommand = new SqlCommand(insertUserSql, databaseConnection))
                        {
                            insertCommand.Parameters.AddWithValue("@applicationUserId", newUser.UserId);
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

                    return generatedUserId;
                }

                System.Diagnostics.Debug.WriteLine($"Found existing user with ID {generatedUserId}");
                return generatedUserId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EnsureUserExists: {ex.Message}");
                // Return a default user ID instead of throwing to allow the flow to continue
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// checks if a default role exists in the database; If none exists, create one along with basic permissions.
        /// Returns the Guid of the found or newly created role.
        /// </summary>
        private Guid EnsureDefaultRoleExists()
        {
            try
            {
                using (var databaseConnection = DrinkDbConnectionHelper.GetConnection())
                {
                    // Attempt to retrieve an existing role
                    const string checkRoleSql = "SELECT TOP 1 roleId FROM Roles";
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
                    string createPermissionSql = @"
                        INSERT INTO Permissions (permissionId, permissionName, resource, action)
                        VALUES (@permissionId, @permissionName, @resource, @action)";
                    using (var createPermissionCommand = new SqlCommand(createPermissionSql, databaseConnection))
                    {
                        createPermissionCommand.Parameters.AddWithValue("@permissionId", newPermissionId);
                        createPermissionCommand.Parameters.AddWithValue("@permissionName", "Basic Access");
                        createPermissionCommand.Parameters.AddWithValue("@resource", "general");
                        createPermissionCommand.Parameters.AddWithValue("@action", "read");
                        createPermissionCommand.ExecuteNonQuery();
                    }

                    // Step 2: Create a role that references that permission.
                    Guid newRoleId = Guid.NewGuid();
                    string createRoleSql = @"
                      INSERT INTO Roles (roleId, roleName, permissionId)
                      VALUES (@newRoleId, @roleName, @permissionId)";
                    using (var createRoleCommand = new SqlCommand(createRoleSql, databaseConnection))
                    {
                        createRoleCommand.Parameters.AddWithValue("@roleId", newRoleId);
                        createRoleCommand.Parameters.AddWithValue("@roleName", "User");
                        createRoleCommand.Parameters.AddWithValue("@permissionId", newPermissionId);
                        createRoleCommand.ExecuteNonQuery();
                    }

                    // Step 3: Create a role-permission mapping (RolePermissions table) - Link Role to Permision.
                    const string linkRolePermissionSql = @"
                           INSERT INTO RolePermissions (roleId, permissionId) 
                           VALUES (@roleId, @permissionId)";
                    using (var linkCommand = new SqlCommand(linkRolePermissionSql, databaseConnection))
                    {
                        linkCommand.Parameters.AddWithValue("@roleId", newRoleId);
                        linkCommand.Parameters.AddWithValue("@permissionId", newPermissionId);
                        linkCommand.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine($"Created new role with ID: {newRoleId}");
                    return newRoleId;
                }
            }
            catch (Exception sqlException)
            {
                // If we fail to create/find a role, return a new GUID instead of stopping the flow.
                System.Diagnostics.Debug.WriteLine($"Error in EnsureDefaultRoleExists: {sqlException.Message}");
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

            // Load from config. If not found, placeholders are used.
            ClientId = System.Configuration.ConfigurationManager.AppSettings["TwitterClientId"]
                           ?? "YOUR_CLIENT_ID";
            ClientSecret = System.Configuration.ConfigurationManager.AppSettings["TwitterClientSecret"]
                             ?? "YOUR_CLIENT_SECRET";

            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientId: {ClientId}");
            System.Diagnostics.Debug.WriteLine($"Loaded Twitter ClientSecret: {ClientSecret.Substring(0, Math.Min(4, ClientSecret.Length))}... (not used in PKCE)");
        }

        /// <summary>
        /// Simple token check method (not heavily used in PKCE flows).
        /// </summary>
        public AuthenticationResult Authenticate(string userId, string token)
        {
            return new AuthenticationResult
            {
                IsAuthenticationSuccessful = !string.IsNullOrEmpty(token),
                OAuthToken = token,
                SessionId = Guid.Empty,
                NewAccount = false
            };
        }

        /// <summary>
        /// Constructs the Twitter OAuth 2.0 authorization URL,
        /// including PKCE challenge and required query parameters.
        /// </summary>
        public string GetAuthorizationUrl()
        {
            // Generate PKCE values (code_verifier and code_challenge)
            var (generatedCodeVerifier, generatedCodeChallenge) = GeneratePkceData();
            codeVerifier = generatedCodeVerifier;  // We'll need this later to exchange for a token.

            // Combine scopes into a space-seperated string.
            var requestedScopes = string.Join(" ", scopes);

            // Define required query parameters for Twitter OAUTH 2.0.
            var queryParameters = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "response_type", "oauthResult" },
                { "scope", requestedScopes },
                { "state", Guid.NewGuid().ToString() },

                // PKCE parameters
                { "code_challenge", generatedCodeChallenge },
                { "code_challenge_method", "S256" }
            };

            // Encode them into a query string.
            var queryString = string.Join("&", queryParameters
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

            var authorizationUrl = $"{AuthorizationEndpoint}?{queryString}";
            System.Diagnostics.Debug.WriteLine($"Generated authorization URL: {authorizationUrl}");
            return authorizationUrl;
        }

        /// <summary>
        /// Exchanges the  authorization Code for an access token.
        /// This request uses the PKCE code_verifier.
        /// </summary>
        public async Task<AuthenticationResult> ExchangeCodeForTokenAsync(string code)
        {
            // Step 1: HTTP POST Prepare token request form fields for exchanging authorization Code with
            // Twitter's token endpoint.
            var tokenExchangeParameters = new Dictionary<string, string>
            {
                { "oauthResult", code },
                { "client_id", ClientId },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" },
                { "code_verifier", codeVerifier }, // PKCE requirement
            };

            System.Diagnostics.Debug.WriteLine("Exchanging oauthResult for token (PKCE).");
            foreach (var tokenExchangeParameter in tokenExchangeParameters)
            {
                System.Diagnostics.Debug.WriteLine($" Token Parameter - Key: {tokenExchangeParameter.Key}, Value: {tokenExchangeParameter.Value}");
            }

            try
            {
                // Send the token exchange request to Twitter and read the raw response.
                using var content = new FormUrlEncodedContent(tokenExchangeParameters);
                var tokenResponse = await httpClient.PostAsync(TokenEndpoint, content);
                var rawResponseContent = await tokenResponse.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"Token Response status: {tokenResponse.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Token Response content: {rawResponseContent}");

                // If the HTTP POST to Twitter failed, log the error and return an unsuccessful AuthenticationResult
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("Token request failed: Twitter returned a non-success HTTP status oauthResult.");
                    return new AuthenticationResult
                    {
                        IsAuthenticationSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }

                // Attempt to deserialize the token response.
                TwitterTokenResponse? tokenResponseModel;
                try
                {
                    tokenResponseModel = await tokenResponse.Content.ReadFromJsonAsync<TwitterTokenResponse>();
                }
                catch (Exception deserializationException)
                {
                    // Fallback for any deserialization issues.
                    System.Diagnostics.Debug.WriteLine($"Error deserializing token response: {deserializationException.Message}");
                    tokenResponseModel = System.Text.Json.JsonSerializer.Deserialize<TwitterTokenResponse>(rawResponseContent);
                }

                // If there is no valid access token, return a failure.
                if (tokenResponseModel == null || string.IsNullOrEmpty(tokenResponseModel.AccessToken))
                {
                    System.Diagnostics.Debug.WriteLine("No access token in tokenResponseModel.");
                    return new AuthenticationResult
                    {
                        IsAuthenticationSuccessful = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }

                // Optionally retrieve user info from Twitter using the access token.
                try
                {
                    using var twitterUserInfoClient = new HttpClient();
                    twitterUserInfoClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponseModel.AccessToken);

                    System.Diagnostics.Debug.WriteLine($"Making request to Twitter user info endpoint: {UserInfoEndpoint}");
                    var twiterUserInfoResponse = await twitterUserInfoClient.GetAsync(UserInfoEndpoint);
                    var twitterUserInfoJson = await twiterUserInfoResponse.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine($"Twitter user info response status: {twiterUserInfoResponse.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"Twitter user info response body: {twitterUserInfoJson}");

                    if (!twiterUserInfoResponse.IsSuccessStatusCode)
                    {
                        // Even if user info fails, the token itself might still be valid.
                        System.Diagnostics.Debug.WriteLine($"User info request failed. Response: {twitterUserInfoJson}");
                        return new AuthenticationResult
                        {
                            IsAuthenticationSuccessful = false,
                            OAuthToken = tokenResponseModel.AccessToken,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }

                    try
                    {
                        // Parse the JSON response into our user model.
                        var twitterUserInfoObject = System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(twitterUserInfoJson);
                        System.Diagnostics.Debug.WriteLine($"Authenticated user: {twitterUserInfoObject?.UserData.EmailAddress} ({twitterUserInfoObject?.UserData.DisplayName})");

                        if (twitterUserInfoObject == null)
                        {
                            // If parsing user info fails, return a negative result.
                            System.Diagnostics.Debug.WriteLine("Failed to deserialize user info response");
                            return new AuthenticationResult
                            {
                                IsAuthenticationSuccessful = false,
                                OAuthToken = tokenResponseModel.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }

                        // Twitter might not always provide an userEmail. We use the user's ID or fallback.
                        string twitterUserId = twitterUserInfoObject?.UserData.TwitterUserId ?? twitterUserInfoObject?.UserData.EmailAddress ?? "unknown";
                        System.Diagnostics.Debug.WriteLine($"Using twitterUserId: {twitterUserId} for user creation");

                        try
                        {
                            // If Twitter doesn't return an userEmail, we create a placeholder using the username.
                            string userEmail = twitterUserInfoObject?.UserData.EmailAddress;
                            if (string.IsNullOrEmpty(userEmail))
                            {
                                // Fallback: build a fake userEmail from username if needed.
                                userEmail = $"{twitterUserInfoObject?.UserData.TwitterUsername ?? "unknown"}@twitter.com";
                                System.Diagnostics.Debug.WriteLine($"No userEmail provided by Twitter, using fallback: {userEmail}");
                            }

                            // Check or create the user in the local DB.
                            var applicationUserId = EnsureUserExists(twitterUserId, userEmail, twitterUserInfoObject?.UserData.DisplayName ?? "Unknown User");
                            System.Diagnostics.Debug.WriteLine($"User ID after EnsureUserExists: {applicationUserId}");

                            // Create a sessionDetails for the user.
                            try
                            {
                                var sessionDetails = SessionAdapterInstance.CreateSession(applicationUserId);
                                System.Diagnostics.Debug.WriteLine($"Session created with ID: {sessionDetails.sessionId}");

                                // Return a insertSucceded response with a valid sessionDetails.
                                return new AuthenticationResult
                                {
                                    IsAuthenticationSuccessful = true,
                                    OAuthToken = tokenResponseModel.AccessToken,
                                    SessionId = sessionDetails.sessionId,
                                    NewAccount = false
                                };
                            }
                            catch (Exception sessionCreationException)
                            {
                                // If sessionDetails creation fails, still inform the client with partial info.
                                System.Diagnostics.Debug.WriteLine($"Error creating sessionDetails: {sessionCreationException.Message}");
                                return new AuthenticationResult
                                {
                                    IsAuthenticationSuccessful = false,
                                    OAuthToken = tokenResponseModel.AccessToken,
                                    SessionId = Guid.Empty,
                                    NewAccount = false
                                };
                            }
                        }
                        catch (Exception userCreationException)
                        {
                            // If user creation fails, return an unsuccessful response.
                            System.Diagnostics.Debug.WriteLine($"Error in EnsureUserExists: {userCreationException.Message}");
                            return new AuthenticationResult
                            {
                                IsAuthenticationSuccessful = false,
                                OAuthToken = tokenResponseModel.AccessToken,
                                SessionId = Guid.Empty,
                                NewAccount = false
                            };
                        }
                    }
                    catch (Exception userInfoDeserializationException)
                    {
                        // In case we cannot deserialize user info properly.
                        System.Diagnostics.Debug.WriteLine($"Error deserializing user info: {userInfoDeserializationException.Message}");
                        return new AuthenticationResult
                        {
                            IsAuthenticationSuccessful = false,
                            OAuthToken = tokenResponseModel.AccessToken,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }
                }
                catch (Exception userInfoFetchException)
                {
                    // If we can't fetch user info but got a valid token, let the user proceed with partial data.
                    System.Diagnostics.Debug.WriteLine($"Exception fetching user info: {userInfoFetchException.Message}");
                    return new AuthenticationResult
                    {
                        IsAuthenticationSuccessful = true,
                        OAuthToken = tokenResponseModel.AccessToken,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }
            }
            catch (Exception exceptionDuringTokenExchange)
            {
                // Catch any unexpected errors during token exchange.
                System.Diagnostics.Debug.WriteLine($"ExchangeCodeForTokenAsync exception: {exceptionDuringTokenExchange.Message}");
                return new AuthenticationResult
                {
                    IsAuthenticationSuccessful = false,
                    OAuthToken = string.Empty,
                    SessionId = Guid.Empty,
                    NewAccount = false,
                };
            }
        }

        /// <summary>
        /// Initiates the Twitter OAuth 2.0 login flow inside a WebView2 dialog.
        /// Navigates to the Twitter authorization URL and intercepts the redirect containing the authorization code.
        /// Once the code is extracted, it is exchanged for an access token and used to authenticate the user.
        /// </summary>
        public async Task<AuthenticationResult> SignInWithTwitterAsync(Window parentWindow)
        {
            var oauthFlowCompletionSource = new TaskCompletionSource<AuthenticationResult>();

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
                    oauthFlowCompletionSource.SetResult(new AuthenticationResult
                    {
                        IsAuthenticationSuccessful = false,
                        OAuthToken = string.Empty,
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
        /// Retricves a value from a query string embedded in a URL.
        /// Throws an error if the specified key is not found.
        /// </summary>
        private string ExtractQueryParameter(string fullUrl, string parameterKey)
        {
            var parsedUri = new Uri(fullUrl);
            var rawQuery = parsedUri.Query.TrimStart('?');
            var queryPairs = rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in queryPairs)
            {
                var keyValuePairs = pair.Split('=', 2);
                if (keyValuePairs.Length == 2 && keyValuePairs[0] == parameterKey)
                {
                    return Uri.UnescapeDataString(keyValuePairs[1]);
                }
            }
            throw new ArgumentException($"Parameter '{parameterKey}' not found in URL: {fullUrl}", nameof(parameterKey));
        }

        /// <summary>
        /// Generates a PKCE-compliant code verifier and correspoinding SHA256 code challenge.
        /// Returns both as base64url-encoded strings for OAuth request and token exchage.
        /// </summary>
        private (string codeVerifier, string codeChallenge) GeneratePkceData()
        {
            // Create a random array of bytes and then Base64Url-encode them to get a code verifier.
            var secureRandom = RandomNumberGenerator.Create();
            var randomBytes = new byte[32];
            secureRandom.GetBytes(randomBytes);

            // Convert to a safe string for the OAuth request (no +, /, or =).
            var generatedVerifier = Convert.ToBase64String(randomBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Create a code_challenge by hashing the code_verifier with SHA256.
            using (var sha256Hasher = SHA256.Create())
            {
                var hashedVerifier = sha256Hasher.ComputeHash(Encoding.UTF8.GetBytes(generatedVerifier));
                var generatedChallenge = Convert.ToBase64String(hashedVerifier)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');

                return (generatedVerifier, generatedChallenge);
            }
        }

        /// <summary>
        /// Decodes and extracts user information from an ID token (JWT) returned by Twitter.
        /// Assumes the token is valid and well-formed
        /// </summary>
        private TwitterUserInfoResponse ExtractUserInfoFromIdToken(string idToken)
        {
            var jwtparts = idToken.Split('.');
            if (jwtparts.Length != 3)
            {
                throw new ArgumentException("Invalid ID token format.", nameof(idToken));
            }

            var payloadSegment = jwtparts[1];
            while (payloadSegment.Length % 4 != 0)
            {
                payloadSegment += '=';
            }

            var payloadBytes = Convert.FromBase64String(payloadSegment.Replace('-', '+').Replace('_', '/'));
            var jsonPayload = Encoding.UTF8.GetString(payloadBytes);

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return System.Text.Json.JsonSerializer.Deserialize<TwitterUserInfoResponse>(jsonPayload, jsonOptions)
                   ?? throw new Exception("Failed to deserialize ID token payloadSegment.");
        }
    }

    /// <summary>
    /// Data model matching Twitter's token response fields.
    /// Includes optional fields like refresh_token and id_token.
    /// </summary>
    internal class TwitterTokenResponse
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public required string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public required string Scope { get; set; }

        // Included only if requested and granted offline access.
        [JsonPropertyName("refresh_token")]
        public required string RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public required string IdToken { get; set; }
    }

    /// <summary>
    /// Model for user info returned by Twitter's "/2/users/me" endpoint.
    /// </summary>
    internal class TwitterUserInfoResponse
    {
        [JsonPropertyName("data")]
        public required TwitterUserProfileData UserData { get; set; }
    }

    /// <summary>
    /// Detailed user data from Twitter's user info response.
    /// Fields that rely on special permissions (e.g., userEmail) will only be present if your app is authorized.
    /// </summary>
    internal class TwitterUserProfileData
    {
        [JsonPropertyName("id")]
        public required string TwitterUserId { get; set; }

        [JsonPropertyName("fullName")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("username")]
        public required string TwitterUsername { get; set; }

        [JsonPropertyName("profile_image_url")]
        public required string ProfileImageUrl { get; set; }

        [JsonPropertyName("userEmail")]
        public required string EmailAddress { get; set; }

        [JsonPropertyName("verified")]
        public bool IsAccountVerified { get; set; }
    }
}
