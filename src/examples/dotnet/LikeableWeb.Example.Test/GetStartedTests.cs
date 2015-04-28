using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using RestSharp;
using VoidPointer.LikeableWeb.Contract.Declarations;
using VoidPointer.LikeableWeb.Contract.NewsFeed;

namespace LikeableWeb.Example.Test
{
    [TestFixture]
    public class GetStartedTests
    {
        private RestClient _client;
        private readonly List<NewsFeed> _newsFeeds = new List<NewsFeed>();
        private const string SeparatorLine = "--------------";

        [SetUp]
        public void Setup()
        {
            _client = new RestClient(TestSetting.TestBaseUrl)
            {
                Authenticator = new HttpBasicAuthenticator(TestSetting.TestAppClientId, TestSetting.TestAppClientSecretId)
            };

            _newsFeeds.Clear();
        }

        [Test]
        public void Fetch_all_data_using_next_starttick()
        {
            long startTick = 0;

            while (true)
            {
                var nextStartTick = RequestNewsFeeds(startTick);
                if (nextStartTick <= 0 || startTick == nextStartTick)
                {
                    Console.WriteLine("No more data to fetch. StartTick: {0}, NextStartTick: {1}",
                                                startTick, 
                                                nextStartTick);
                    break;
                }

                // Note: Next starttick should be saved in storage (example CMS at the root-node-level) to use next time (scheduled task) or
                // if something goes wrong. This so you do not need to run everything again from start and can continue where 
                // the previous request and saving data were successfully.

                startTick = nextStartTick;
                Console.WriteLine("Fetch complete! Next starttick: {0}", nextStartTick);
            }

            Console.WriteLine(SeparatorLine);

            Console.WriteLine("Number of News Feeds From Facebook : {0}", _newsFeeds.Count(nf => nf.SourceSystem == SourceSystem.Facebook));
            Console.WriteLine("Number of News Feeds From Instagram : {0}", _newsFeeds.Count(nf => nf.SourceSystem == SourceSystem.Instagram));
            Console.WriteLine("Number of News Feeds From YouTube : {0}", _newsFeeds.Count(nf => nf.SourceSystem == SourceSystem.Youtube));
            
            Console.WriteLine(SeparatorLine);

            Console.WriteLine("Total Number of News Feeds: {0}", _newsFeeds.Count);

            var count = 1;
            _newsFeeds.ForEach(nf =>
                {
                    if (_newsFeeds.Count(feed => feed.LikeableWebFeedId == nf.LikeableWebFeedId) != 1)
                    {
                        Assert.Fail("Duplicate feeds! Should not be possible!");
                    }

                    Console.WriteLine(SeparatorLine);
                    Console.WriteLine("Feed No: {0}", count);
                    Console.WriteLine(nf);
                    Console.WriteLine("Number of attached photos: {0}", nf.Photos.Count());
                    Console.WriteLine("Number of attached videos: {0}", nf.Videos.Count());

                    foreach (var photo in nf.Photos)
                    {
                        Console.WriteLine(photo);
                    }
                    
                    count++;

                });
        }

        private long RequestNewsFeeds(long startTick)
        {
            var request = new RestRequest("/newsfeed")
                {
                    RequestFormat = DataFormat.Json
                };

            request.AddObject(new NewsFeedRequest()
                {
                    StartTick = startTick
                });

            var response = _client.Execute(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                // We use NewtonSoft to deserialize since the lists/arrays in the contract, not always work with the internal deserialization in 
                // restsharp
                var newsFeedResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<NewsFeedResponse>(response.Content);
                foreach (var newsFeed in newsFeedResponse.NewsFeeds)
                {
                    // Simulate replace item in db if such exists by comparing the PostIds. Note: We always use the newest item!
                    // The reason to use PostId and not the LikeableWebFeedId as key, is that LikeableWeb is capable to have multiple streams 
                    // in the same application, that could contain the same post from same page but in two separate streams. 
                    // We want it to be just one post in the final aggregated view.

                    var existingItem = _newsFeeds.FirstOrDefault(nf => nf.PostId == newsFeed.PostId);
                    if (existingItem != null)
                    {
                        _newsFeeds.Remove(existingItem);    // We simply just remove the old one from list and then insert the new one!
                    }
                    _newsFeeds.Add(newsFeed);
                    
                }
                return newsFeedResponse.NextStartTick;  // Return next timepointer, for the next request!
            }
            
            var message = String.Format("Request failed. Status Code: {0}, ErrorMessage: {1}, Exception: {2}",
                                        response.StatusCode,
                                        response.ErrorMessage,
                                        response.ErrorException != null ? response.ErrorException.Message : "no exception!");

            Assert.Fail(message);
            return -1;
        }
    }
}
