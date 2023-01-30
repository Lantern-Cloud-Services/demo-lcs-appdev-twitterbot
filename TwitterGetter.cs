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
using Newtonsoft.Json;
using System.Collections.Generic;


namespace Lcs.TwitterPoster
{
    public static class TwitterGetter
    {
        public static ILogger sLog;


        [FunctionName("TwitterGetter")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            sLog = log;
            sLog.LogInformation("C# HTTP trigger function processed a request.");
            
            String requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            StateHelper stateHelper = JsonConvert.DeserializeObject<StateHelper>(requestBody);

            if (!_checkHelperState(stateHelper))
            {
                ResultsHelper emptyResult = new ResultsHelper();
                emptyResult.stateHelper = stateHelper;
                
                return new OkObjectResult(JsonConvert.SerializeObject(emptyResult));
            }

            ResultsHelper resultsHelper = _getTweetsForHashTags(stateHelper.lastProcessedId);

            if (null == resultsHelper || resultsHelper.data == null)
            {
                ResultsHelper emptyResult = new ResultsHelper();
                emptyResult.stateHelper = stateHelper;
                emptyResult.data = new List<DataHelper>();
                emptyResult.meta = new MetaHelper();
                emptyResult.meta.newest_id = "";
                emptyResult.meta.oldest_id = "";
                emptyResult.meta.result_count = "0";
                
                return new OkObjectResult(JsonConvert.SerializeObject(emptyResult));
            }

            sLog.LogInformation("Found hashtag results update stateHelper");
            resultsHelper.stateHelper = stateHelper;
            stateHelper.lastProcessedId = resultsHelper.meta.newest_id;

            List<DataHelper> processList = _processTweets(stateHelper, resultsHelper);

            if (processList.Count < 1)
            {
                // no hashtag results return only the state helper
                ResultsHelper emptyResult = new ResultsHelper();
                emptyResult.stateHelper = stateHelper;
                emptyResult.data = new List<DataHelper>();
                emptyResult.meta = new MetaHelper();
                emptyResult.meta.newest_id = "";
                emptyResult.meta.oldest_id = "";
                emptyResult.meta.result_count = "0";

                stateHelper.state = "complete";
                
                return new OkObjectResult(JsonConvert.SerializeObject(emptyResult));
            }

            foreach (DataHelper dh in processList)
            {
                DateTime dateTime = DateTime.ParseExact(dh.created_at, "yyyy-MM-ddTHH:mm:ss.000Z", System.Globalization.CultureInfo.InvariantCulture);

                TimeSpan t = dateTime - new DateTime(1970, 1, 1);
                long epochMilis = (long)t.TotalMilliseconds;
                sLog.LogInformation("DateTime: " + epochMilis);

                if (stateHelper.lastProcessedTimeStamp < epochMilis)
                {
                    stateHelper.lastProcessedTimeStamp = epochMilis;
                }
            }

            ResultsHelper result = new ResultsHelper();
            result.meta = resultsHelper.meta;
            result.data = processList;
            result.stateHelper = stateHelper;
            
            return new OkObjectResult(JsonConvert.SerializeObject(result));
        }

        private static ResultsHelper _getTweetsForHashTags(String aReqSince)
        {
            String URL = System.Environment.GetEnvironmentVariable($"twitterGETURL");

            String reqQueryName  = "query";
            String reqQuery      = System.Environment.GetEnvironmentVariable($"reqQuery");
            String reqSinceName  = "since_id";
            
            // rework since, if longer than 7 seven days ommit
            String reqSince      = aReqSince;
            String reqFieldsName = "tweet.fields";
            String reqFields     = System.Environment.GetEnvironmentVariable($"reqFields");

            OAuthUtils oaUtils = new OAuthUtils();            
            OAuth1Authenticator oAuth1 = oaUtils.GetOAuth1Authenticator();

            RestClient client = new RestClient(URL);
            client.Authenticator = oAuth1;
            
            RestRequest request = new RestRequest(URL, Method.Get);
            request.AddHeader("Content-Type", "application/json");  
                    
            request.AddQueryParameter(reqQueryName, reqQuery);
            
            if (null != aReqSince && !aReqSince.Equals(""))
            {
                request.AddQueryParameter(reqSinceName, reqSince);                
            }
            request.AddQueryParameter(reqFieldsName, reqFields);
            RestResponse response = client.Execute(request);

            if (null!= response && null != response.Content)
            {
                return JsonConvert.DeserializeObject<ResultsHelper>(response.Content);
            }

            return null;
        }

        private static List<DataHelper> _processTweets(StateHelper aStateHelper, ResultsHelper aResultsHelper)
        {
            String[] strlist = aStateHelper.hashTags.Split(",");
            List<DataHelper> processList = new List<DataHelper>();
            foreach (DataHelper dHelper in aResultsHelper.data)
            {
                Boolean found = false;
                foreach (String hashTag in strlist)
                {
                    if (dHelper.text.Contains(hashTag))
                    {
                        String userId = dHelper.author_id;                        
                        dHelper.author_id = _getUserNameForID(userId).data.username;
                        
                        processList.Add(dHelper);
                        found = true;
                        break;
                    }

                    if (found)
                    {
                        continue;
                    }
                }
            }

            return processList;
        }

        private static Boolean _checkHelperState(StateHelper aStateHelper)
        {
            sLog.LogInformation("Checking helper state");
            
            // check TTL
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long unixTimeMilliseconds = now.ToUnixTimeMilliseconds();
            
            long lastActionTimeStamp = aStateHelper.lastProcessedTimeStamp;
            long millisSinceLastAction = unixTimeMilliseconds - lastActionTimeStamp; 
            sLog.LogInformation("Millis since last action: " + millisSinceLastAction);
            
            if (millisSinceLastAction > 604800100)
            {
                aStateHelper.lastProcessedId = "";
            }
            
            if (aStateHelper.state.Equals("in progress"))
            {
                //sLog.LogInformation("Processor is in progress!");
                
                if (aStateHelper.stateTimeStamp + aStateHelper.ttl > unixTimeMilliseconds)
                {
                    sLog.LogInformation("A processor job is still running within TTL");

                    // a job is still running and within TTL                    
                    return false;
                }

                sLog.LogInformation("A processor ran over TTL, reprossing");
            }

            sLog.LogInformation("Previous job complete, marking this job in progress");
            aStateHelper.stateTimeStamp = unixTimeMilliseconds;
            aStateHelper.state = "in progress";

            return true;
        }

        private static userDataHelper _getUserNameForID(String aUserID)
        {
            String URL = System.Environment.GetEnvironmentVariable($"twitterGETUsernameURL") + aUserID;

            String reqFieldsName = "user.fields";
            String reqFields     = "username";

            OAuthUtils oaUtils = new OAuthUtils();            
            OAuth1Authenticator oAuth1 = oaUtils.GetOAuth1Authenticator();

            RestClient client = new RestClient(URL);
            client.Authenticator = oAuth1;
            
            RestRequest request = new RestRequest(URL, Method.Get);
            request.AddHeader("Content-Type", "application/json");  
                    
            request.AddQueryParameter(reqFieldsName, reqFields);
            RestResponse response = client.Execute(request);

            if (null!= response && null != response.Content)
            {
                return JsonConvert.DeserializeObject<userDataHelper>(response.Content);
            }

            return null;
        }        
    }
}
