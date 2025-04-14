﻿using System;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using Windows.ApplicationModel;

namespace DrinkDb_Auth.OAuthProviders
{
    public class FacebookOAuthHelper : IFacebookOAuthHelper
    {
        private static readonly FacebookOAuth2Provider facebookOAuth2Provider = new();
        private static readonly SessionAdapter sessionAdapter = new();
        private static string GetPackageSecurityIdentifier()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var packageSid = packageId.FamilyName;
            return $"ms-app://s-1-15-2-{packageSid}";
        }

        private const string ClientId = "667671795847732";
        private string RedirectUri = "http://localhost:8888/auth";
        private const string Scope = "email";
        private string BuildAuthorizeUrl()
        {
            Console.WriteLine($"RedirectUri: {RedirectUri}");
            return $"https://www.facebook.com/v22.0/dialog/oauth?client_id={ClientId}" +
                   $"&display=popup" +
                   $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                   $"&response_type=token&scope={Scope}";
        }

        private TaskCompletionSource<AuthenticationResponse> _tcs;

        public FacebookOAuthHelper()
        {
            _tcs = new TaskCompletionSource<AuthenticationResponse>();
            FacebookLocalOAuthServer.OnTokenReceived += OnTokenReceived;
        }

        private void OnTokenReceived(string accessToken)
        {
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                AuthenticationResponse res = facebookOAuth2Provider.Authenticate(string.Empty, accessToken);
                
                _tcs.TrySetResult(res);
            }
        }

        public async Task<AuthenticationResponse> AuthenticateAsync()
        {
            _tcs = new TaskCompletionSource<AuthenticationResponse>();

            var authorizeUri = new Uri(BuildAuthorizeUrl());

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authorizeUri.ToString(),
                UseShellExecute = true
            });

            AuthenticationResponse response = await _tcs.Task;
            return response;
        }
    }
}
