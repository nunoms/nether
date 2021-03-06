﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Net;
using IdentityModel.Client;
using System.Security.Authentication;
using Microsoft.VisualStudio.TestTools.LoadTesting;
using NetherLoadTest.Helpers;

namespace NetherLoadTest
{
    [TestClass]
    public class NetherUnitTest
    {
        private NetherClient _client;
        private Random _random = new Random();

        public TestContext TestContext { get; set; }



        ////////////////////////////////////////////////////////
        // Read-only properties that expose the load test config

        public string BaseUrl
        {
            get { return (string)TestContext.Properties["BaseUrl"]; }
        }
        public string ClientId
        {
            get { return (string)TestContext.Properties["ClientId"]; }
        }
        public string ClientSecret
        {
            get { return (string)TestContext.Properties["ClientSecret"]; }
        }
        public string AdminUserName
        {
            get { return (string)TestContext.Properties["AdminUserName"]; }
        }
        public string AdminPassword
        {
            get { return (string)TestContext.Properties["AdminPassword"]; }
        }


        /////////////////////////////////////////////
        // Test properties that store the test state
        private LoadTestUserContext UserContext
        {
            get
            {
                return TestContext.Properties["$LoadTestUserContext"] as LoadTestUserContext;
            }
        }
        // Saving the access token as we can't serialise the NetherClient
        // Storing this allows us to pick up the access token for the virtual user without logging in again
        public string AccessToken
        {
            get { return UserContext.GetWithDefault("Test_AccessToken", (string)null); }
            set { UserContext["Test_AccessToken"] = value; }
        }
        public bool LoggedIn
        {
            get { return UserContext.GetWithDefault("Test_LoggedIn", false); }
            set { UserContext["Test_LoggedIn"] = value; }
        }

        public string UserName
        {
            get { return UserContext.GetWithDefault("Test_UserName", (string)null); }
            set { UserContext["Test_UserName"] = value; }
        }
        public string Password
        {
            get { return UserContext.GetWithDefault("Test_Password", (string)null); }
            set { UserContext["Test_Password"] = value; }
        }

        [TestInitialize]
        public void Init()
        {
            if (UserName == null)
            {
                UserName = "loadUser" + _random.Next(10000); // hard coded user names created for the load test in the memory store
                Password = "password";
            }
            var baseUrl = BaseUrl ?? "http://localhost:5000";
            _client = new NetherClient(baseUrl, ClientId, ClientSecret);
            if (LoggedIn)
            {
                // we've already logged in so set the access token
                _client.AccessToken = AccessToken;
            }
        }

        [TestMethod]
        public async Task PostScore()
        {
            await EnsureLoggedInAsync();

            TestContext.BeginTimer("PostScore");
            await _client.PostScoreAsync(_random.Next(100, 1000));
            TestContext.EndTimer("PostScore");
        }

        [TestMethod]
        public async Task GetScore()
        {
            await EnsureLoggedInAsync();

            TestContext.BeginTimer("GetScore");
            await _client.GetScoresAsync();
            TestContext.EndTimer("GetScore");
        }

        private async Task EnsureLoggedInAsync()
        {
            if (!LoggedIn)
            {
                // ensure that the user exists!
                await EnsureUserExistsAsync();

                // log in
                await _client.LoginUserNamePasswordAsync(UserName, Password);

                // save the access token
                AccessToken = _client.AccessToken;
                LoggedIn = true;
            }
        }
        private async Task EnsureUserExistsAsync()
        {
            // Log in as admin
            var adminClient = await GetClientAsync(AdminUserName, AdminPassword);

            // Create the user
            var response = await adminClient.PutAsJsonAsync(
                $"/api/identity/users/{UserName}",
                new
                {
                    role = "Player",
                    active = true
                });
            response.EnsureSuccessStatusCode();

            // create login
            response = await adminClient.PutAsJsonAsync(
                $"/api/identity/users/{UserName}/logins/password/{UserName}", // reuse username as gamertag
                new
                {
                    Password
                });

            // sign in as player and create gamertag
            var playerClient = await GetClientAsync(UserName, Password);
            var player = new
            {
                gamertag = UserName,
                country = "UK",
                customTag = "LoadTestUser"
            };
            response = await playerClient.PutAsJsonAsync("api/player", player);
            response.EnsureSuccessStatusCode();
        }

        private async Task<HttpClient> GetClientAsync(string username, string password)
        {
            var discoveryResponse = await DiscoveryClient.GetAsync(BaseUrl);
            if (discoveryResponse.TokenEndpoint == null)
            {
                throw new AuthenticationException("GetClient: could not discover endpoint, server is offline?");
            }

            var tokenClient = new TokenClient(discoveryResponse.TokenEndpoint, ClientId, ClientSecret);
            var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync(username, password, "nether-all");
            if (tokenResponse.IsError)
            {
                throw new AuthenticationException($"GetClient: failed to authenticate: '{tokenResponse.ErrorDescription}'");
            }

            // Create HttpClient with admin token
            var client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };

            client.SetBearerToken(tokenResponse.AccessToken);
            return client;
        }
    }
}
