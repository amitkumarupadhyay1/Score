using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Score
{
    public class PounceSubmission
    {
        public string TeamName { get; set; }
        public string Answer { get; set; }
        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
        public bool Evaluated { get; set; }
        public bool IsCorrect { get; set; }
        public int PointsAwarded { get; set; }
    }

    public sealed partial class QuizBuzzerServer
    {
        private readonly object _pounceLock = new object();
        private readonly List<PounceSubmission> _pounceSubmissions = new List<PounceSubmission>();

        public volatile bool PounceEnabled;

        public List<PounceSubmission> PounceSubmissions
        {
            get
            {
                lock (_pounceLock)
                {
                    return new List<PounceSubmission>(_pounceSubmissions);
                }
            }
        }

        public event Action<PounceSubmission> PounceReceived;
        public event Action OnPouncesReset;

        public void ResetPounces()
        {
            lock (_pounceLock)
            {
                _pounceSubmissions.Clear();
            }
            try
            {
                OnPouncesReset?.Invoke();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("QuizBuzzerServer OnPouncesReset error.", ex);
            }
        }

        internal bool HandlePounceHttpRequest(string path, HttpListenerRequest req, HttpListenerResponse res)
        {
            if (path.Equals("/api/state", StringComparison.OrdinalIgnoreCase))
            {
                string queryTeam = req.QueryString["team"];
                string stateJson = BuildStateJson(queryTeam);
                byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = stateBytes.Length;
                res.OutputStream.Write(stateBytes, 0, stateBytes.Length);
                res.Close();
                return true;
            }

            if (path.Equals("/api/pounce", StringComparison.OrdinalIgnoreCase))
            {
                if (req.HttpMethod == "GET")
                {
                    string stateJson = BuildStateJson(req.QueryString["team"]);
                    byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = stateBytes.Length;
                    res.OutputStream.Write(stateBytes, 0, stateBytes.Length);
                    res.Close();
                    return true;
                }

                if (req.HttpMethod == "POST")
                {
                    string body = string.Empty;
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                    {
                        body = reader.ReadToEnd();
                    }

                    var outcome = SubmitPounce(body, out PounceSubmission submission, out string errorMsg);
                    string jsonResponse;
                    if (outcome == PounceResult.Success)
                    {
                        res.StatusCode = 200;
                        jsonResponse = "{\"status\":\"ok\",\"message\":\"Pounce submitted! Awaiting host adjudication.\",\"team\":\"" +
                                       EscapeJson(submission.TeamName) + "\",\"answer\":\"" + EscapeJson(submission.Answer) + "\"}";
                    }
                    else if (outcome == PounceResult.Disabled)
                    {
                        res.StatusCode = 423;
                        res.StatusDescription = "Locked";
                        jsonResponse = "{\"status\":\"locked\",\"error\":\"" + EscapeJson(errorMsg) + "\"}";
                    }
                    else if (outcome == PounceResult.Duplicate)
                    {
                        res.StatusCode = 409;
                        res.StatusDescription = "Conflict";
                        jsonResponse = "{\"status\":\"duplicate\",\"error\":\"" + EscapeJson(errorMsg) + "\"}";
                    }
                    else
                    {
                        res.StatusCode = 400;
                        res.StatusDescription = "Bad Request";
                        jsonResponse = "{\"status\":\"error\",\"error\":\"" + EscapeJson(errorMsg) + "\"}";
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(jsonResponse);
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = bytes.Length;
                    res.OutputStream.Write(bytes, 0, bytes.Length);
                    res.Close();
                    return true;
                }
            }

            return false;
        }

        internal bool HandlePounceTcpRequest(NetworkStream stream, string method, string path, string rawUrl, string body)
        {
            if (path.Equals("/api/state", StringComparison.OrdinalIgnoreCase))
            {
                string queryTeam = ExtractQueryParameter(rawUrl, "team");
                string stateJson = BuildStateJson(queryTeam);
                WriteTcpResponse(stream, "200 OK", "application/json; charset=utf-8", stateJson);
                return true;
            }

            if (path.Equals("/api/pounce", StringComparison.OrdinalIgnoreCase))
            {
                if (method == "GET")
                {
                    string queryTeam = ExtractQueryParameter(rawUrl, "team");
                    string stateJson = BuildStateJson(queryTeam);
                    WriteTcpResponse(stream, "200 OK", "application/json; charset=utf-8", stateJson);
                    return true;
                }

                if (method == "POST")
                {
                    var outcome = SubmitPounce(body, out PounceSubmission submission, out string errorMsg);
                    if (outcome == PounceResult.Success)
                    {
                        string okJson = "{\"status\":\"ok\",\"message\":\"Pounce submitted! Awaiting host adjudication.\",\"team\":\"" +
                                        EscapeJson(submission.TeamName) + "\",\"answer\":\"" + EscapeJson(submission.Answer) + "\"}";
                        WriteTcpResponse(stream, "200 OK", "application/json; charset=utf-8", okJson);
                    }
                    else if (outcome == PounceResult.Disabled)
                    {
                        string lockedJson = "{\"status\":\"locked\",\"error\":\"" + EscapeJson(errorMsg) + "\"}";
                        WriteTcpResponse(stream, "423 Locked", "application/json; charset=utf-8", lockedJson);
                    }
                    else if (outcome == PounceResult.Duplicate)
                    {
                        string dupJson = "{\"status\":\"duplicate\",\"error\":\"" + EscapeJson(errorMsg) + "\"}";
                        WriteTcpResponse(stream, "409 Conflict", "application/json; charset=utf-8", dupJson);
                    }
                    else
                    {
                        string errJson = "{\"status\":\"error\",\"error\":\"" + EscapeJson(errorMsg) + "\"}";
                        WriteTcpResponse(stream, "400 Bad Request", "application/json; charset=utf-8", errJson);
                    }
                    return true;
                }
            }

            return false;
        }

        public enum PounceResult
        {
            Success,
            Disabled,
            Duplicate,
            InvalidInput
        }

        public PounceResult SubmitPounce(string payload, out PounceSubmission submission, out string message)
        {
            submission = null;
            if (!PounceEnabled)
            {
                message = "Chit Pounce is currently disabled by the host.";
                return PounceResult.Disabled;
            }

            ExtractPounceFields(payload, out string team, out string answer);
            if (string.IsNullOrWhiteSpace(team))
            {
                message = "Team name cannot be empty.";
                return PounceResult.InvalidInput;
            }
            if (string.IsNullOrWhiteSpace(answer))
            {
                message = "Secret answer cannot be empty.";
                return PounceResult.InvalidInput;
            }

            team = team.Trim();
            answer = answer.Trim();

            lock (_pounceLock)
            {
                if (_pounceSubmissions.Any(s => string.Equals(s.TeamName, team, StringComparison.OrdinalIgnoreCase)))
                {
                    message = $"Team '{team}' has already submitted a pounce for this question.";
                    return PounceResult.Duplicate;
                }

                submission = new PounceSubmission
                {
                    TeamName = team,
                    Answer = answer,
                    SubmittedAtUtc = DateTime.UtcNow,
                    Evaluated = false,
                    IsCorrect = false,
                    PointsAwarded = 0
                };
                _pounceSubmissions.Add(submission);
            }

            try
            {
                PounceReceived?.Invoke(submission);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("QuizBuzzerServer PounceReceived event trigger error.", ex);
            }

            message = "Pounce submitted! Awaiting host adjudication.";
            return PounceResult.Success;
        }

        public string BuildStateJson(string teamName = null)
        {
            bool hasPounced = false;
            int count;
            lock (_pounceLock)
            {
                count = _pounceSubmissions.Count;
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    hasPounced = _pounceSubmissions.Any(s => string.Equals(s.TeamName, teamName.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"isArmed\":").Append(IsBuzzerArmed ? "true" : "false").Append(",");
            sb.Append("\"pounceEnabled\":").Append(PounceEnabled ? "true" : "false").Append(",");
            sb.Append("\"hasPounced\":").Append(hasPounced ? "true" : "false").Append(",");
            sb.Append("\"pounceCount\":").Append(count);
            sb.Append("}");
            return sb.ToString();
        }

        private static void ExtractPounceFields(string body, out string team, out string answer)
        {
            team = null;
            answer = null;
            if (string.IsNullOrWhiteSpace(body)) return;

            string trimmed = body.Trim();
            if (trimmed.StartsWith("{"))
            {
                team = ExtractJsonStringValue(trimmed, "team");
                answer = ExtractJsonStringValue(trimmed, "answer");
                if (string.IsNullOrWhiteSpace(answer))
                {
                    answer = ExtractJsonStringValue(trimmed, "text");
                }
                return;
            }

            var parts = trimmed.Split('&');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                {
                    string key = kv[0].Trim();
                    string val = Uri.UnescapeDataString(kv[1].Trim().Replace('+', ' '));
                    if (key.Equals("team", StringComparison.OrdinalIgnoreCase)) team = val;
                    if (key.Equals("answer", StringComparison.OrdinalIgnoreCase) || key.Equals("text", StringComparison.OrdinalIgnoreCase)) answer = val;
                }
            }
        }

        private static string ExtractJsonStringValue(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName)) return null;

            int propIdx = json.IndexOf("\"" + propertyName + "\"", StringComparison.OrdinalIgnoreCase);
            if (propIdx < 0) return null;

            int colonIdx = json.IndexOf(':', propIdx);
            if (colonIdx < 0) return null;

            int q1 = json.IndexOf('"', colonIdx);
            if (q1 < 0) return null;

            var sb = new StringBuilder();
            bool escaped = false;
            for (int i = q1 + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    if (c == 'n') sb.Append('\n');
                    else if (c == 'r') sb.Append('\r');
                    else if (c == 't') sb.Append('\t');
                    else sb.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string ExtractQueryParameter(string rawUrl, string paramName)
        {
            if (string.IsNullOrWhiteSpace(rawUrl) || !rawUrl.Contains("?")) return null;
            var qs = rawUrl.Substring(rawUrl.IndexOf('?') + 1);
            foreach (var param in qs.Split('&'))
            {
                var kv = param.Split('=');
                if (kv.Length == 2 && kv[0].Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1].Replace('+', ' '));
                }
            }
            return null;
        }
    }
}
