using System;
using System.Collections.Generic;


namespace Lcs.TwitterPoster
{
   public class ResultsHelper
    {
        public List<DataHelper> data {get; set;}
        public MetaHelper meta {get; set;}

        public StateHelper stateHelper {get; set;}
    }
    public class DataHelper
    {
        public String id {get; set;}
        public String author_id {get; set;}
        public List<String> edit_history_tweet_ids {get; set;}
        public String created_at {get; set;}
        public String text {get; set;}
    }
    public class MetaHelper
    {
        public String newest_id {get; set;}
        public String oldest_id {get; set;}
        public String result_count {get; set;}
    }

    public class StateHelper
    {
        public String lastProcessedId {get; set;}
        public long lastProcessedTimeStamp {get; set;}
        public String state {get; set;}
        public long stateTimeStamp {get; set;}
        public int ttl {get; set;}
        public String hashTags {get; set;}
    }

    public class userHelper
    {
        public String id {get; set;}
        public String name {get; set;}
        public String username {get; set;}
    }

    public class userDataHelper
    {
        public userHelper data {get; set;}
    }
}