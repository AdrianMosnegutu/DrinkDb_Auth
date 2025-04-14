using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrinkDb_Auth.OAuthProviders;
using DrinkDb_Auth.Service;

namespace Tests
{
    [TestClass]
    public sealed class AuthenticationService_Tests
    {

        [TestMethod]
        public async Task AuthWithOAuthTestGoogle()
        {
            AuthenticationService service = new AuthenticationService(new MockLinkedInServer(), new MockGitHubServer(), new MockFacebookServer(), new MockUserAdapter(), new MockSessionAdapter());
            var google = new MockGoogleAuthProvider();
            var id = Guid.NewGuid();
            google.MockId = id;
            var response = await service.AuthWithOAuth(null, OAuthService.Google, google);

            AuthResponse authResponse = new AuthResponse { AuthSuccessful = false, NewAccount = false, OAuthToken = string.Empty, SessionId = id };

            Assert.AreEqual(response, authResponse);
        }
    }
}
