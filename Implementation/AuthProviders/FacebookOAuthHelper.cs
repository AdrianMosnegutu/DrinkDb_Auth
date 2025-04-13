using System;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using Windows.ApplicationModel;

namespace DrinkDb_Auth.OAuthProviders
{
    public class FacebookOAuthHelper
    {
        private static readonly FacebookOAuth2Provider FacebookOAuth2Provider = new ();
        private static readonly SessionAdapter SessionAdapter = new ();
        private TaskCompletionSource<AuthenticationResponse> tcs;

        private const string ClientId = "667671795847732";
        private string redirectUri = "http://localhost:8888/auth";
        private const string Scope = "email";

        public FacebookOAuthHelper()
        {
            tcs = new TaskCompletionSource<AuthenticationResponse>();
            FacebookLocalOAuthServer.OnTokenReceived += OnTokenReceived;
        }

        private static string GetPackageSecurityIdentifier()
        {
            Package package = Package.Current;
            var packageId = package.Id;
            var packageSid = packageId.FamilyName;
            return $"ms-app://s-1-15-2-{packageSid}";
        }

        private string BuildAuthorizeUrl()
        {
            Console.WriteLine($"RedirectUri: {redirectUri}");
            return $"https://www.facebook.com/v22.0/dialog/oauth?client_id={ClientId}" +
                   $"&display=popup" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                   $"&response_type=token&scope={Scope}";
        }

        private void OnTokenReceived(string accessToken)
        {
            if (tcs != null && !tcs.Task.IsCompleted)
            {
                AuthenticationResponse res = FacebookOAuth2Provider.Authenticate(string.Empty, accessToken);
                tcs.TrySetResult(res);
            }
        }

        public async Task<AuthenticationResponse> AuthenticateAsync()
        {
            tcs = new TaskCompletionSource<AuthenticationResponse>();

            var authorizeUri = new Uri(BuildAuthorizeUrl());

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authorizeUri.ToString(),
                UseShellExecute = true
            });

            AuthenticationResponse response = await tcs.Task;
            return response;
        }
    }
}