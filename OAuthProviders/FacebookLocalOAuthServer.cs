using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DrinkDb_Auth.OAuthProviders
{
    public class FacebookLocalOAuthServer
    {
        private readonly HttpListener _listener;

        public static event Action<string> OnTokenReceived;

        public FacebookLocalOAuthServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine("Serverul local ascultă la: " + string.Join(", ", _listener.Prefixes));

            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    if (context.Request.Url.AbsolutePath.Equals("/auth", StringComparison.OrdinalIgnoreCase))
                    {
                        string responseHtml = GetHtmlResponse();
                        byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.ContentType = "text/html; charset=utf-8";
                        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    }
                    else if (context.Request.Url.AbsolutePath.Equals("/token", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        {
                            string queryParameters = await reader.ReadToEndAsync();
                            if (queryParameters.StartsWith("#"))
                            {
                                queryParameters = queryParameters.Substring(1);
                            }

                            var splitParameters = HttpUtility.ParseQueryString(queryParameters.Trim());
                            string accessToken = splitParameters["access_token"];
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                Console.WriteLine("Token de acces: " + accessToken);

                                OnTokenReceived?.Invoke(accessToken);
                            }
                            else
                            {
                                Console.WriteLine("Token de acces nu a fost găsit.");
                            }
                        }
                        context.Response.StatusCode = 200;
                        context.Response.OutputStream.Close();
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        context.Response.OutputStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Eroare: " + ex.Message);
                    break;
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
        }

        private string GetHtmlResponse()
        {
            return @"
<!DOCTYPE html>
<html>
  <head>
    <title>OAuth Log in successful!</title>
    <script>
        window.onload = () => {
            console.log('HI!');
            fetch('http://localhost:8888/token', {
                method: 'POST',
                headers: {
                    'Content-Type': 'text/plain'
                },
                body: window.location.hash
            });
        }
     </script>
  </head>
  <body>
    <h1>Autentificare cu succes!</h1>
    <p id='message'>Poti inchide aceasta pagina.</p>
  </body>
</html>";
        }
    }
}
