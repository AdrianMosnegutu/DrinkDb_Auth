using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GitHubLocalOAuthServer
    {
        private readonly HttpListener _listener;

        /// <summary>
        /// Fires when the GitHub code is received from the redirect.
        /// </summary>
        public static event Action<string>? OnCodeReceived;

        private bool _isRunning = false;

        public GitHubLocalOAuthServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _listener.Start();
            Console.WriteLine("GitHub local OAuth server listening on: " + string.Join(", ", _listener.Prefixes));

            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.Url == null)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.OutputStream.Close();
                        continue;
                    }
                    if (context.Request.Url.AbsolutePath.Equals("/auth", StringComparison.OrdinalIgnoreCase))
                    {
                        // GitHub redirects here with ?code=... 
                        // We'll show a minimal HTML that triggers a POST to /exchange with the code
                        string code = HttpUtility.ParseQueryString(context.Request.Url.Query).Get("code") ?? throw new Exception("Code not found in request.");

                        string responseHtml = GetHtmlResponse(code);
                        byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.ContentType = "text/html; charset=utf-8";
                        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    }
                    else if (context.Request.Url.AbsolutePath.Equals("/exchange", StringComparison.OrdinalIgnoreCase)
                             && context.Request.HttpMethod == "POST")
                    {
                        // We read the 'code' from the request body and notify any subscribers
                        using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        {
                            string code = (await reader.ReadToEndAsync()).Trim();
                            if (!string.IsNullOrEmpty(code))
                            {
                                Console.WriteLine("GitHub code received: " + code);
                                OnCodeReceived?.Invoke(code);
                            }
                            else
                            {
                                Console.WriteLine("No GitHub code found.");
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
                    Console.WriteLine("Error in GitHubLocalOAuthServer: " + ex.Message);
                    break;
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }

        private string GetHtmlResponse(string code)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                  <head>
                    <title>GitHub OAuth Login Successful!</title>
                    <script>
                        window.onload = () => {{
                            fetch('http://localhost:8890/exchange', {{
                                method: 'POST',
                                headers: {{
                                    'Content-Type': 'text/plain'
                                }},
                                body: '{code}'
                            }});
                        }}
                     </script>
                  </head>
                  <body>
                    <h1>Authentication successful!</h1>
                    <p>You can close this tab.</p>
                  </body>
                </html>";
        }
    }
}
