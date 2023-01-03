using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Authenticators.OAuth;
namespace Lcs.TwitterPoster
{
    public class OAuthUtils
    {
        String URL            = System.Environment.GetEnvironmentVariable($"twitterGETURL");
        String consumerKey    = System.Environment.GetEnvironmentVariable($"consumerKey");
        String consumerSecret = System.Environment.GetEnvironmentVariable($"consumerSecret");
        String token          = System.Environment.GetEnvironmentVariable($"token");
        String tokenSecret    = System.Environment.GetEnvironmentVariable($"tokenSecret");    

        public OAuth1Authenticator GetOAuth1Authenticator()
        {
                OAuth1Authenticator oAuth1 = OAuth1Authenticator.ForAccessToken(
                    consumerKey: consumerKey,
                    consumerSecret: consumerSecret,
                    token: token,
                    tokenSecret: tokenSecret,
                    OAuthSignatureMethod.HmacSha256);

            return oAuth1;
        }

    }
}