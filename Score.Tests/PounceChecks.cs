using System;
using System.IO;
using System.Net;
using System.Threading;

namespace Score.Tests
{
    public static class PounceChecks
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Pounce Check Failed: " + message);
            }
        }

        public static void Run()
        {
            // 1. Verify PounceSubmission model
            var now = DateTime.UtcNow;
            var sub = new PounceSubmission
            {
                TeamName = "Team Titan",
                Answer = "Apollo 11",
                SubmittedAtUtc = now,
                Evaluated = false,
                IsCorrect = false,
                PointsAwarded = 0
            };
            Assert(sub.TeamName == "Team Titan", "TeamName mismatch");
            Assert(sub.Answer == "Apollo 11", "Answer mismatch");
            Assert(!sub.Evaluated, "Evaluated should default to false");
            Assert(!sub.IsCorrect, "IsCorrect should default to false");
            Assert(sub.PointsAwarded == 0, "PointsAwarded should default to 0");

            // 2. Verify QuizBuzzerServer Pounce lifecycle in-memory
            var server = new QuizBuzzerServer(8096);
            Assert(!server.PounceEnabled, "PounceEnabled should default to false");
            Assert(server.PounceSubmissions.Count == 0, "PounceSubmissions should initially be empty");

            // Attempt submitting when disabled
            var disabledResult = server.SubmitPounce("{\"team\":\"Team Titan\",\"answer\":\"Apollo 11\"}", out var subOut, out var errorMsg);
            Assert(disabledResult == QuizBuzzerServer.PounceResult.Disabled, "SubmitPounce should be Disabled when PounceEnabled is false");
            Assert(subOut == null, "subOut should be null on disabled submission");

            // Enable pounce mode
            server.PounceEnabled = true;
            Assert(server.PounceEnabled, "PounceEnabled should be true");

            // Submit empty team
            var emptyResult = server.SubmitPounce("{\"team\":\"\",\"answer\":\"Apollo 11\"}", out _, out _);
            Assert(emptyResult == QuizBuzzerServer.PounceResult.InvalidInput, "Empty team should be InvalidInput");

            // Submit valid pounce
            PounceSubmission capturedEventSub = null;
            server.PounceReceived += s => { capturedEventSub = s; };

            var successResult = server.SubmitPounce("{\"team\":\"Team Titan\",\"answer\":\"Apollo 11\"}", out subOut, out errorMsg);
            Assert(successResult == QuizBuzzerServer.PounceResult.Success, "SubmitPounce should succeed");
            Assert(subOut != null && subOut.TeamName == "Team Titan" && subOut.Answer == "Apollo 11", "Submission details mismatch");
            Assert(capturedEventSub != null && capturedEventSub.TeamName == "Team Titan", "PounceReceived event not fired");
            Assert(server.PounceSubmissions.Count == 1, "PounceSubmissions count should be 1");

            // Submit duplicate pounce for same team
            var dupResult = server.SubmitPounce("{\"team\":\"Team Titan\",\"answer\":\"Gemini 8\"}", out var dupSub, out var dupErr);
            Assert(dupResult == QuizBuzzerServer.PounceResult.Duplicate, "Duplicate submission by same team must be rejected");
            Assert(server.PounceSubmissions.Count == 1, "Pounce count must remain 1 after duplicate attempt");

            // Submit pounce for a second team
            var secondResult = server.SubmitPounce("{\"team\":\"Team Apex\",\"answer\":\"Sputnik 1\"}", out var secondSub, out _);
            Assert(secondResult == QuizBuzzerServer.PounceResult.Success, "Second team submission should succeed");
            Assert(server.PounceSubmissions.Count == 2, "PounceSubmissions count should now be 2");

            // Verify State JSON output
            string titanState = server.BuildStateJson("Team Titan");
            Assert(titanState.Contains("\"pounceEnabled\":true"), "State JSON should indicate pounceEnabled: true");
            Assert(titanState.Contains("\"hasPounced\":true"), "Team Titan state JSON should indicate hasPounced: true");

            string otherTeamState = server.BuildStateJson("Team Echo");
            Assert(otherTeamState.Contains("\"hasPounced\":false"), "Team Echo state JSON should indicate hasPounced: false");

            // Verify ResetPounces
            bool resetFired = false;
            server.OnPouncesReset += () => { resetFired = true; };
            server.ResetPounces();
            Assert(server.PounceSubmissions.Count == 0, "ResetPounces should clear submissions list");
            Assert(resetFired, "OnPouncesReset event should be raised");

            string afterResetState = server.BuildStateJson("Team Titan");
            Assert(afterResetState.Contains("\"hasPounced\":false"), "After reset, hasPounced should be false");
            Assert(afterResetState.Contains("\"pounceCount\":0"), "After reset, pounceCount should be 0");

            // 3. Test Web Assets HTML & JS
            string mobileHtml = QuizBuzzerWebAssets.GetMobileHtml();
            Assert(mobileHtml.Contains("pounceCard"), "Web assets must contain pounceCard");
            Assert(mobileHtml.Contains("pounceInput"), "Web assets must contain pounceInput");
            Assert(mobileHtml.Contains("pounceBtn"), "Web assets must contain pounceBtn");
            Assert(mobileHtml.Contains("POUNCE"), "Web assets must contain POUNCE button text");
            Assert(mobileHtml.Contains("Secret Answer"), "Web assets must contain Secret Answer placeholder");
            Assert(mobileHtml.Contains("/api/pounce"), "Web assets must contain /api/pounce route");
            Assert(mobileHtml.Contains("/api/state"), "Web assets must contain /api/state route");
            Assert(mobileHtml.Contains("Pounce submitted! Awaiting host adjudication."), "Web assets must contain confirmation message");

            // 4. Test View Model Item
            var testSub = new PounceSubmission { TeamName = "Team Falcon", Answer = "Jupiter", Evaluated = false };
            var vm = new PounceSubmissionItem(testSub, bonus: 20, penalty: 10);
            Assert(vm.CanEvaluate, "Un-evaluated item should be evaluatable");
            Assert(vm.CorrectButtonLabel == "✓ (+20)", "CorrectButtonLabel should format bonus");
            Assert(vm.IncorrectButtonLabel == "✗ (-10)", "IncorrectButtonLabel should format penalty");
            Assert(vm.StatusLabel.Contains("Awaiting"), "StatusLabel should indicate awaiting adjudication");

            // Evaluate correct
            testSub.Evaluated = true;
            testSub.IsCorrect = true;
            testSub.PointsAwarded = 20;
            Assert(!vm.CanEvaluate, "Evaluated item should not be evaluatable");
            Assert(vm.StatusLabel.Contains("+20"), "StatusLabel should show +20 for correct");

            // Evaluate incorrect
            testSub.IsCorrect = false;
            testSub.PointsAwarded = -10;
            Assert(vm.StatusLabel.Contains("-10"), "StatusLabel should show -10 for incorrect");

            // 5. Test Live HTTP Server Endpoint for /api/pounce and /api/state
            TestLiveHttpEndpoints();
        }

        private static void TestLiveHttpEndpoints()
        {
            QuizBuzzerServer liveServer = null;
            try
            {
                liveServer = new QuizBuzzerServer(8097);
                liveServer.Start();
                string baseUrl = liveServer.LocalUrl.TrimEnd('/');

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";

                    // GET /api/state initially: pounceEnabled should be false
                    string initialState = client.DownloadString(baseUrl + "/api/state");
                    Assert(initialState.Contains("\"pounceEnabled\":false"), "Initial state must have pounceEnabled: false");

                    // POST /api/pounce while disabled should yield 423 Locked
                    try
                    {
                        client.UploadString(baseUrl + "/api/pounce", "{\"team\":\"Team Alpha\",\"answer\":\"Mars\"}");
                        Assert(false, "Expected WebException (423 Locked) when pounce is disabled");
                    }
                    catch (WebException wex)
                    {
                        if (wex.Response is HttpWebResponse resp)
                        {
                            Assert((int)resp.StatusCode == 423, "Expected HTTP 423 Locked when disabled");
                        }
                    }

                    // Enable pounce on server
                    liveServer.PounceEnabled = true;

                    // GET /api/state with team parameter
                    string stateBeforePounce = client.DownloadString(baseUrl + "/api/state?team=Team+Alpha");
                    Assert(stateBeforePounce.Contains("\"pounceEnabled\":true"), "State should show pounceEnabled: true");
                    Assert(stateBeforePounce.Contains("\"hasPounced\":false"), "State should show hasPounced: false");

                    // POST /api/pounce with JSON
                    string pounceRes = client.UploadString(baseUrl + "/api/pounce", "{\"team\":\"Team Alpha\",\"answer\":\"Mars\"}");
                    Assert(pounceRes.Contains("\"status\":\"ok\""), "Pounce response status should be ok");
                    Assert(pounceRes.Contains("Pounce submitted! Awaiting host adjudication."), "Pounce response should contain confirmation");
                    Assert(liveServer.PounceSubmissions.Count == 1, "Server should have 1 recorded submission");

                    // Duplicate POST /api/pounce should yield 409 Conflict
                    try
                    {
                        client.UploadString(baseUrl + "/api/pounce", "{\"team\":\"Team Alpha\",\"answer\":\"Venus\"}");
                        Assert(false, "Expected WebException (409 Conflict) on duplicate submission");
                    }
                    catch (WebException wex)
                    {
                        if (wex.Response is HttpWebResponse resp)
                        {
                            Assert((int)resp.StatusCode == 409, "Expected HTTP 409 Conflict on duplicate team submission");
                        }
                    }

                    // GET /api/state should now show hasPounced: true
                    string stateAfterPounce = client.DownloadString(baseUrl + "/api/state?team=Team+Alpha");
                    Assert(stateAfterPounce.Contains("\"hasPounced\":true"), "State should show hasPounced: true for Team Alpha");

                    // Reset pounces
                    liveServer.ResetPounces();
                    Assert(liveServer.PounceSubmissions.Count == 0, "Submissions should be empty after reset");

                    string stateAfterReset = client.DownloadString(baseUrl + "/api/state?team=Team+Alpha");
                    Assert(stateAfterReset.Contains("\"hasPounced\":false"), "State should show hasPounced: false after reset");
                }
            }
            finally
            {
                liveServer?.Stop();
                liveServer?.Dispose();
            }
        }
    }
}
