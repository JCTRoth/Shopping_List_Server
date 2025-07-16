using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using ShoppingListServer.Helpers;
using ShoppingListServer.Models;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly AppSettings _appSettings;
        IRestService _restService;

        public AuthenticationService(IOptions<AppSettings> appSettings, IRestService restService)
        {
            _appSettings = appSettings.Value;
            _restService = restService;
        }

        class FacebookAuthenticationResponse
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "email")]
            public string EMail { get; set; }
        }

        public async Task<HttpResponseMessage> AuthenticateFacebook(FacebookProfile facebookProfile, string accessToken)
        {
            // verify access token whith this link:
            // https://graph.facebook.com/debug_token?input_token=EAAHOgab9jbkBAOZAmqu8ekE0y1VZBGFbfsWHBZCOZAW8fo6mAWIkR8uJmAgo4LXn69ImOeURBhZAzYDT4bYyuNqQegOkF3wA2k1JXZBbGghD3ZBDXrt3sqdyDRx6UjeJBa5XuT1qYiSHJXjYZA4oLLUdZBObGwWz3lHEEg9caUxItzZBcZAy52fCCjhid0pR3rDUOutHtp0Ppi0QgkJO9EYni2Ff0pyJwgpqtxAGuYTwWRFDj2YKZAAChP8BEjKXyfZBwKZC0ZD&access_token=508531224448441|c_eL2lzG70c4N-6griSVFv2-f5w
            // https://graph.facebook.com/debug_token?input_token=<user-token>&access_token=508531224448441|c_eL2lzG70c4N-6griSVFv2-f5w
            // verify that user exists
            // https://graph.facebook.com/me?access_token=EAAHOgab9jbkBAOZAmqu8ekE0y1VZBGFbfsWHBZCOZAW8fo6mAWIkR8uJmAgo4LXn69ImOeURBhZAzYDT4bYyuNqQegOkF3wA2k1JXZBbGghD3ZBDXrt3sqdyDRx6UjeJBa5XuT1qYiSHJXjYZA4oLLUdZBObGwWz3lHEEg9caUxItzZBcZAy52fCCjhid0pR3rDUOutHtp0Ppi0QgkJO9EYni2Ff0pyJwgpqtxAGuYTwWRFDj2YKZAAChP8BEjKXyfZBwKZC0ZD

            // access_token=508531224448441
            // graph.facebook.com/debug_token?input_token={token-to-inspect}&access_token={app-token-or-admin-token}
            // https://graph.facebook.com/113716804852420?access_token=508531224448441|c_eL2lzG70c4N-6griSVFv2-f5w

        
            string uriString = "https://graph.facebook.com/" + _appSettings.FacebookAppID + "?access_token=" + accessToken + "?fields=email";
            HttpResponseMessage response = await _restService.CreatePostRequest(uriString, "");
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                FacebookAuthenticationResponse fbResponse = JsonConvert.DeserializeObject<FacebookAuthenticationResponse>(content);
                if (fbResponse != null)
                {
                    if (string.IsNullOrEmpty(fbResponse.EMail) || fbResponse.EMail != facebookProfile.Email)
                    {
                        throw new Exception(StatusMessages.AccountAuthenticationFailed);
                    }
                }
            }
            return response;
        }

        public async Task<HttpResponseMessage> AuthenticateFacebookWithException(FacebookProfile facebookProfile, string accessToken)
        {
            HttpResponseMessage response = await AuthenticateFacebook(facebookProfile, accessToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(StatusMessages.AccountAuthenticationFailed);
            }
            return response;
        }

        class GoogleAuthenticationResponse
        {
            [JsonProperty(PropertyName = "user_id")]
            public string UserId { get; set; }

            [JsonProperty(PropertyName = "email")]
            public string EMail { get; set; }

            [JsonProperty(PropertyName = "verified_email")]
            public bool VerifiedEMail { get; set; }
        }

        public async Task<HttpResponseMessage> AuthenticateGoogle(GoogleUser googleUser, string accessToken)
        {
            // verify access token whith this link:
            // https://www.googleapis.com/oauth2/v1/tokeninfo?access_token=

            string uriString = "https://www.googleapis.com/oauth2/v1/tokeninfo?access_token=" + accessToken;
            HttpResponseMessage response = await _restService.CreatePostRequest(uriString, "");

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                GoogleAuthenticationResponse googleResponse = JsonConvert.DeserializeObject<GoogleAuthenticationResponse>(content);
                if (googleResponse != null)
                {
                    if (string.IsNullOrEmpty(googleResponse.EMail) || googleResponse.EMail != googleUser.Email)
                    {
                        throw new Exception(StatusMessages.AccountAuthenticationFailed);
                    }
                }
            }

            return response;
        }

        public async Task<HttpResponseMessage> AuthenticateGoogleWithException(GoogleUser googleUser, string accessToken)
        {
            HttpResponseMessage response = await AuthenticateGoogle(googleUser, accessToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(StatusMessages.AccountAuthenticationFailed);
            }
            return response;
        }

        class AppleAuthenticationToken
        {
            [JsonProperty(PropertyName = "access_token")]
            public string AccessToken { get; set; }

            [JsonProperty(PropertyName = "token_type")]
            public string TokenTypen { get; set; }

            [JsonProperty(PropertyName = "expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty(PropertyName = "refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty(PropertyName = "id_token")]
            public string IdToken { get; set; }
        }

        public async Task<HttpResponseMessage> AuthenticateApple(AppleAccount appleAccount)
        {
            // https://developer.apple.com/documentation/sign_in_with_apple/generate_and_validate_tokens
            // POST https://appleid.apple.com/auth/token
            // client_id=GenericListApp.iOS.app
            // client_secret=
            // grant_type=refresh_token
            // refresh_token=appleAccount.Token

            //var assembly = Assembly.GetExecutingAssembly();
            //string p8FileContents = "";
            //using (Stream stream = assembly.GetManifestResourceStream(_appSettings.AppleSignInP8SecretResourcePath))
            //{
            //    using (StreamReader reader = new StreamReader(stream))
            //    {
            //        p8FileContents = reader.ReadToEnd();
            //    }
            //}
            string p8FileContents = await System.IO.File.ReadAllTextAsync(_appSettings.AppleSignInP8SecretPath);

            HttpResponseMessage response = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            string authenticatedEMail = "";
            bool emailVerified = false;
            string sub = "";
            if (!string.IsNullOrEmpty(p8FileContents))
            {
                string secret = GenerateClientSecretJWT(p8FileContents);
                FormUrlEncodedContent content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", appleAccount.AuthorizationCode },
                    { "client_id", _appSettings.AppleClientID },
                    { "client_secret", secret },
                });

                HttpClient client = _restService.GetClient();
                var header = new ProductHeaderValue("Xamarin", "1.0");
                var userAgentHeader = new ProductInfoHeaderValue(header);
                client.DefaultRequestHeaders.UserAgent.Add(userAgentHeader);

                response = await client.PostAsync("https://appleid.apple.com/auth/token", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    AppleAuthenticationToken authenticationToken = JsonConvert.DeserializeObject<AppleAuthenticationToken>(responseContent);
                    if (authenticationToken != null)
                    {
                        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                        SecurityToken jsonToken = handler.ReadToken(authenticationToken.IdToken);
                        JwtSecurityToken tokenS = jsonToken as JwtSecurityToken;

                        // Example:
                        // {
                        //  "iss": "https://appleid.apple.com",
                        //  "aud": "GenericListApp.iOS.app",
                        //  "exp": 1681302687,
                        //  "iat": 1681216287,
                        //  "sub": "xxxxxx.yyyyyy.zzzzzz",
                        //  "at_hash": "Gv_P_lDpLSUoG_fhfPeQzg",
                        //  "email": "<name>@icloud.com",
                        //  "email_verified": "true",
                        //  "auth_time": 1681216156,
                        //  "nonce_supported": true,
                        //  "real_user_status": 2
                        // }

                        Claim emailClaim = tokenS.Claims.FirstOrDefault(claim => claim.Type == "email");
                        if (emailClaim != null)
                        {
                            authenticatedEMail = emailClaim.Value;
                        }
                        Claim emailVerifiedClaim = tokenS.Claims.FirstOrDefault(claim => claim.Type == "email_verified");
                        if (emailVerifiedClaim != null)
                        {
                            emailVerified = emailVerifiedClaim.Value == "true";
                        }
                        Claim subClaim = tokenS.Claims.FirstOrDefault(claim => claim.Type == "sub");
                        if (subClaim != null)
                        {
                            sub = subClaim.Value;
                        }
                    }
                }
            }
            bool userIdValidation = !string.IsNullOrEmpty(sub) && sub == appleAccount.UserId;
            bool emailValidation = !string.IsNullOrEmpty(authenticatedEMail) && authenticatedEMail == appleAccount.Email && emailVerified;
            if (!userIdValidation && !emailValidation)
            {
                throw new Exception(StatusMessages.AccountAuthenticationFailed);
            }

            return response;
        }

        public async Task<HttpResponseMessage> AuthenticateAppleWithException(AppleAccount appleAccount)
        {
            HttpResponseMessage response = await AuthenticateApple(appleAccount);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(StatusMessages.AccountAuthenticationFailed);
            }
            return response;
        }

        public string GenerateClientSecretJWT(string p8FileContents)
        {
            var now = new DateTimeOffset(DateTime.UtcNow);

            string keyId = _appSettings.AppleSignInKeyId;
            string teamId = _appSettings.AppleTeamId;
            string serverId = _appSettings.AppleClientID;

            var headers = new Dictionary<string, object>
                {
                    {  "alg", "ES256" },
                    {  "kid", keyId }
                };

            var payload = new Dictionary<string, object>
                {
                    { "iss", teamId },
                    { "iat", now.ToUnixTimeSeconds() },
                    { "exp", now.AddHours(1).ToUnixTimeSeconds() },
                    { "aud", "https://appleid.apple.com"},
                    { "sub", serverId }
                };

            //var secretKeyLines = p8FileContents.Split('\n').ToList();
            //var secretKey = string.Concat(secretKeyLines.Where(l =>
            //                    !l.StartsWith("--", StringComparison.OrdinalIgnoreCase)
            //                    && !l.EndsWith("--", StringComparison.OrdinalIgnoreCase)));

            var p8privateKey = CleanP8Key(p8FileContents);

            // Get our headers/payloads into a json string
            var headerStr = "{" + string.Join(",", headers.Select(kvp => $"\"{kvp.Key}\":\"{kvp.Value.ToString()}\"")) + "}";
            var payloadStr = "{";
            foreach (var kvp in payload)
            {
                if (kvp.Value is int || kvp.Value is long || kvp.Value is double)
                    payloadStr += $"\"{kvp.Key}\":{kvp.Value.ToString()},";
                else
                    payloadStr += $"\"{kvp.Key}\":\"{kvp.Value.ToString()}\",";
            }
            payloadStr = payloadStr.TrimEnd(',') + "}";


            // Load the key text
            //var key = CngKey.Import(Convert.FromBase64String(secretKey), CngKeyBlobFormat.Pkcs8PrivateBlob);

            //using (var dsa = new ECDsaCng(key))
            //{
            //    var unsignedJwt = Base64UrlEncode(Encoding.UTF8.GetBytes(headerStr))
            //                            + "." + Base64UrlEncode(Encoding.UTF8.GetBytes(payloadStr));

            //    var signature = dsa.SignData(Encoding.UTF8.GetBytes(unsignedJwt), HashAlgorithmName.SHA256);

            //    return unsignedJwt + "." + Base64UrlEncode(signature);
            //}

            using (ECDsa key = ECDsa.Create())
            {
                key.ImportPkcs8PrivateKey(Convert.FromBase64String(p8privateKey), out _);

                var unsignedJwt = Base64UrlEncode(Encoding.UTF8.GetBytes(headerStr))
                    + "." + Base64UrlEncode(Encoding.UTF8.GetBytes(payloadStr));
                //byte[] encodedRequest = Encoding.UTF8.GetBytes(unsignedJwt);
                var signature = key.SignData(Encoding.UTF8.GetBytes(unsignedJwt), HashAlgorithmName.SHA256);
                //string signature = Base64UrlEncode(key.SignData(encodedRequest, HashAlgorithmName.SHA256));

                return $"{unsignedJwt}.{Base64UrlEncode(signature)}";
            }

        }

        static string CleanP8Key(string p8Contents)
        {
            // Remove whitespace
            var tmp = Regex.Replace(p8Contents, "\\s+", string.Empty, RegexOptions.Singleline);

            // Remove `---- BEGIN PRIVATE KEY ----` bits
            tmp = Regex.Replace(tmp, "-{1,}.*?-{1,}", string.Empty, RegexOptions.Singleline);

            return tmp;
        }

        static string Base64UrlEncode(byte[] data)
        {
            var base64 = Convert.ToBase64String(data, 0, data.Length);
            var base64Url = new StringBuilder();

            foreach (var c in base64)
            {
                if (c == '+')
                    base64Url.Append('-');
                else if (c == '/')
                    base64Url.Append('_');
                else if (c == '=')
                    break;
                else
                    base64Url.Append(c);
            }

            return base64Url.ToString();
        }
    }
}
