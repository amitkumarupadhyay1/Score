using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Score
{
    public sealed class QuizBuzzerServer : IDisposable
    {
        public const int DefaultPort = 8088;
        private HttpListener _httpListener;
        private TcpListener _tcpListener;
        private Thread _workerThread;
        private volatile bool _isRunning;
        private readonly object _lock = new object();

        public volatile bool IsBuzzerArmed = true;
        public string LastBuzzedTeam { get; private set; }
        public long LastBuzzStopwatchTicks { get; private set; }
        public long LastBuzzUtcTicks { get; private set; }

        public int Port { get; private set; }
        public string LocalIp { get; }
        public string LocalUrl => $"http://{LocalIp}:{Port}/";
        public bool IsRunning => _isRunning;

        public event Action<string> OnBuzzReceived;
        public event Action<string, long, long> OnBuzzReceivedDetailed;
        public event Action<string, long> OnBuzzReceivedWithTimestamp;
        public Func<IEnumerable<string>> GetTeamsCallback { get; set; }

        public QuizBuzzerServer(int port = DefaultPort)
        {
            Port = port;
            LocalIp = GetLocalIpAddress();
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                bool started = false;
                int[] candidatePorts = { Port, Port + 1, Port + 2, Port + 10 };

                foreach (int testPort in candidatePorts)
                {
                    // Try HttpListener first with wildcard if permissions allow
                    try
                    {
                        _httpListener = new HttpListener();
                        _httpListener.Prefixes.Add($"http://*:{testPort}/");
                        _httpListener.Start();
                        Port = testPort;
                        started = true;
                        break;
                    }
                    catch
                    {
                        try
                        {
                            if (_httpListener != null)
                            {
                                _httpListener.Close();
                                _httpListener = null;
                            }
                        }
                        catch { }
                    }

                    // Try TcpListener on IPAddress.Any for Wi-Fi mobile clients without elevation
                    try
                    {
                        _tcpListener = new TcpListener(IPAddress.Any, testPort);
                        _tcpListener.Start();
                        Port = testPort;
                        started = true;
                        break;
                    }
                    catch
                    {
                        try
                        {
                            if (_tcpListener != null)
                            {
                                _tcpListener.Stop();
                                _tcpListener = null;
                            }
                        }
                        catch { }

                        // Fallback: try localhost HttpListener
                        try
                        {
                            _httpListener = new HttpListener();
                            _httpListener.Prefixes.Add($"http://localhost:{testPort}/");
                            _httpListener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
                            _httpListener.Start();
                            Port = testPort;
                            started = true;
                            break;
                        }
                        catch
                        {
                            try
                            {
                                if (_httpListener != null)
                                {
                                    _httpListener.Close();
                                    _httpListener = null;
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (started)
                {
                    _isRunning = true;
                    _workerThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "QuizBuzzerServerWorker"
                    };
                    _workerThread.Start();
                }
                else
                {
                    AppDiagnostics.WriteException("Failed to bind QuizBuzzerServer on candidate ports starting at " + Port, new IOException("Port binding failed"));
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                try
                {
                    if (_httpListener != null && _httpListener.IsListening)
                    {
                        _httpListener.Stop();
                        _httpListener.Close();
                    }
                }
                catch { }
                finally
                {
                    _httpListener = null;
                }

                try
                {
                    if (_tcpListener != null)
                    {
                        _tcpListener.Stop();
                    }
                }
                catch { }
                finally
                {
                    _tcpListener = null;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ListenLoop()
        {
            if (_httpListener != null && _httpListener.IsListening)
            {
                while (_isRunning)
                {
                    try
                    {
                        var context = _httpListener.GetContext();
                        long swTicks = Stopwatch.GetTimestamp();
                        long utcTicks = DateTime.UtcNow.Ticks;
                        ThreadPool.QueueUserWorkItem(_ => ProcessHttpContext(context, swTicks, utcTicks));
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!_isRunning) break;
                        AppDiagnostics.WriteException("QuizBuzzerServer HttpListener error.", ex);
                    }
                }
            }
            else if (_tcpListener != null)
            {
                while (_isRunning)
                {
                    try
                    {
                        var client = _tcpListener.AcceptTcpClient();
                        long swTicks = Stopwatch.GetTimestamp();
                        long utcTicks = DateTime.UtcNow.Ticks;
                        ThreadPool.QueueUserWorkItem(_ => ProcessTcpClient(client, swTicks, utcTicks));
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!_isRunning) break;
                        AppDiagnostics.WriteException("QuizBuzzerServer TcpListener error.", ex);
                    }
                }
            }
        }

        private void ProcessHttpContext(HttpListenerContext context, long swTicks, long utcTicks)
        {
            try
            {
                var req = context.Request;
                var res = context.Response;

                // Add CORS headers for mobile browser compatibility
                res.AddHeader("Access-Control-Allow-Origin", "*");
                res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

                if (req.HttpMethod == "OPTIONS")
                {
                    res.StatusCode = 200;
                    res.Close();
                    return;
                }

                var path = (req.Url.AbsolutePath ?? "/").TrimEnd('/');
                if (string.IsNullOrEmpty(path)) path = "/";

                if (path == "/" || path == "/index.html")
                {
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(GetMobileHtml());
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentLength64 = htmlBytes.Length;
                    res.OutputStream.Write(htmlBytes, 0, htmlBytes.Length);
                    res.Close();
                    return;
                }

                if (path.Equals("/api/teams", StringComparison.OrdinalIgnoreCase))
                {
                    var teams = GetTeamsCallback != null ? GetTeamsCallback() : new[] { "Team 1", "Team 2", "Team 3", "Team 4" };
                    var json = "[" + string.Join(",", teams.Select(t => "\"" + EscapeJson(t) + "\"")) + "]";
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = bytes.Length;
                    res.OutputStream.Write(bytes, 0, bytes.Length);
                    res.Close();
                    return;
                }

                if (path.Equals("/api/buzz", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsBuzzerArmed)
                    {
                        res.StatusCode = 423;
                        res.StatusDescription = "Locked";
                        var lockedJson = "{\"status\":\"locked\",\"error\":\"Buzzers are disarmed/locked.\"}";
                        byte[] lockedBytes = Encoding.UTF8.GetBytes(lockedJson);
                        res.ContentType = "application/json; charset=utf-8";
                        res.ContentLength64 = lockedBytes.Length;
                        res.OutputStream.Write(lockedBytes, 0, lockedBytes.Length);
                        res.Close();
                        return;
                    }

                    string team = null;

                    if (req.HttpMethod == "POST")
                    {
                        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                        {
                            string body = reader.ReadToEnd();
                            team = ExtractTeamFromJsonOrForm(body);
                        }
                    }
                    else if (req.HttpMethod == "GET")
                    {
                        team = req.QueryString["team"];
                    }

                    if (!string.IsNullOrWhiteSpace(team))
                    {
                        TriggerBuzz(team.Trim(), swTicks, utcTicks);
                    }

                    var json = "{\"status\":\"ok\",\"team\":\"" + EscapeJson(team ?? string.Empty) + "\",\"timestamp\":" + utcTicks + "}";
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = bytes.Length;
                    res.OutputStream.Write(bytes, 0, bytes.Length);
                    res.Close();
                    return;
                }

                res.StatusCode = 404;
                res.Close();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Error handling HTTP buzzer request.", ex);
            }
        }

        private void ProcessTcpClient(TcpClient client, long swTicks, long utcTicks)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    var headerBuffer = new byte[8192];
                    int totalRead = 0;
                    int headerEnd = -1;

                    while (totalRead < headerBuffer.Length)
                    {
                        int read = stream.Read(headerBuffer, totalRead, headerBuffer.Length - totalRead);
                        if (read <= 0) break;
                        totalRead += read;
                        string partial = Encoding.ASCII.GetString(headerBuffer, 0, totalRead);
                        headerEnd = partial.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (headerEnd >= 0) break;
                    }

                    if (headerEnd < 0) return;

                    string headerString = Encoding.ASCII.GetString(headerBuffer, 0, headerEnd);
                    var headerLines = headerString.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    if (headerLines.Length == 0) return;

                    var reqLineParts = headerLines[0].Split(' ');
                    if (reqLineParts.Length < 2) return;

                    string method = reqLineParts[0].ToUpperInvariant();
                    string rawUrl = reqLineParts[1];
                    string path = (rawUrl.Split('?')[0] ?? "/").TrimEnd('/');
                    if (string.IsNullOrEmpty(path)) path = "/";

                    int contentLength = 0;
                    foreach (var line in headerLines)
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(line.Substring(15).Trim(), out contentLength);
                        }
                    }

                    string body = string.Empty;
                    int bodyAlreadyRead = totalRead - (headerEnd + 4);
                    var bodyBytes = new List<byte>();
                    if (bodyAlreadyRead > 0)
                    {
                        int toTake = contentLength > 0 ? Math.Min(bodyAlreadyRead, contentLength) : bodyAlreadyRead;
                        for (int i = 0; i < toTake; i++)
                            bodyBytes.Add(headerBuffer[headerEnd + 4 + i]);
                    }
                    while (contentLength > 0 && bodyBytes.Count < contentLength)
                    {
                        byte[] chunk = new byte[Math.Min(2048, contentLength - bodyBytes.Count)];
                        int r = stream.Read(chunk, 0, chunk.Length);
                        if (r <= 0) break;
                        for (int i = 0; i < r; i++) bodyBytes.Add(chunk[i]);
                    }
                    if (bodyBytes.Count > 0)
                    {
                        body = Encoding.UTF8.GetString(bodyBytes.ToArray());
                    }

                    if (method == "OPTIONS")
                    {
                        WriteTcpResponse(stream, "200 OK", "text/plain", string.Empty);
                        return;
                    }

                    if (path == "/" || path == "/index.html")
                    {
                        WriteTcpResponse(stream, "200 OK", "text/html; charset=utf-8", GetMobileHtml());
                        return;
                    }

                    if (path.Equals("/api/teams", StringComparison.OrdinalIgnoreCase))
                    {
                        var teams = GetTeamsCallback != null ? GetTeamsCallback() : new[] { "Team 1", "Team 2", "Team 3", "Team 4" };
                        var json = "[" + string.Join(",", teams.Select(t => "\"" + EscapeJson(t) + "\"")) + "]";
                        WriteTcpResponse(stream, "200 OK", "application/json; charset=utf-8", json);
                        return;
                    }

                    if (path.Equals("/api/buzz", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsBuzzerArmed)
                        {
                            WriteTcpResponse(stream, "423 Locked", "application/json; charset=utf-8", "{\"status\":\"locked\",\"error\":\"Buzzers are disarmed/locked.\"}");
                            return;
                        }

                        string team = null;
                        if (method == "POST")
                        {
                            team = ExtractTeamFromJsonOrForm(body);
                        }
                        else if (rawUrl.Contains("?"))
                        {
                            var qs = rawUrl.Substring(rawUrl.IndexOf('?') + 1);
                            foreach (var param in qs.Split('&'))
                            {
                                var kv = param.Split('=');
                                if (kv.Length == 2 && kv[0].Equals("team", StringComparison.OrdinalIgnoreCase))
                                {
                                    team = Uri.UnescapeDataString(kv[1].Replace('+', ' '));
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(team))
                        {
                            TriggerBuzz(team.Trim(), swTicks, utcTicks);
                        }

                        var json = "{\"status\":\"ok\",\"team\":\"" + EscapeJson(team ?? string.Empty) + "\",\"timestamp\":" + utcTicks + "}";
                        WriteTcpResponse(stream, "200 OK", "application/json; charset=utf-8", json);
                        return;
                    }

                    WriteTcpResponse(stream, "404 Not Found", "text/plain", "Not Found");
                }
                catch (Exception ex)
                {
                    AppDiagnostics.WriteException("QuizBuzzerServer TCP request error.", ex);
                }
            }
        }

        private static void WriteTcpResponse(NetworkStream stream, string status, string contentType, string content)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(content);
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(status).Append("\r\n");
            sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
            sb.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type\r\n");
            sb.Append("Connection: close\r\n\r\n");
            byte[] headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }
            stream.Flush();
        }

        private void TriggerBuzz(string teamName)
        {
            TriggerBuzz(teamName, Stopwatch.GetTimestamp(), DateTime.UtcNow.Ticks);
        }

        private void TriggerBuzz(string teamName, long swTicks, long utcTicks)
        {
            try
            {
                LastBuzzedTeam = teamName;
                LastBuzzStopwatchTicks = swTicks;
                LastBuzzUtcTicks = utcTicks;

                OnBuzzReceived?.Invoke(teamName);
                OnBuzzReceivedDetailed?.Invoke(teamName, swTicks, utcTicks);
                OnBuzzReceivedWithTimestamp?.Invoke(teamName, swTicks);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("QuizBuzzerServer buzz event trigger failed.", ex);
            }
        }

        private static string ExtractTeamFromJsonOrForm(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            // Check JSON {"team":"Team Alpha"}
            if (body.TrimStart().StartsWith("{"))
            {
                int idx = body.IndexOf("\"team\"", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int colon = body.IndexOf(':', idx);
                    if (colon >= 0)
                    {
                        int q1 = body.IndexOf('"', colon);
                        if (q1 >= 0)
                        {
                            var sb = new StringBuilder();
                            bool escaped = false;
                            for (int i = q1 + 1; i < body.Length; i++)
                            {
                                char c = body[i];
                                if (escaped)
                                {
                                    sb.Append(c);
                                    escaped = false;
                                }
                                else if (c == '\\')
                                {
                                    escaped = true;
                                }
                                else if (c == '"')
                                {
                                    return sb.ToString().Trim();
                                }
                                else
                                {
                                    sb.Append(c);
                                }
                            }
                        }
                    }
                }
            }

            // Check URL-encoded form data team=Team+Alpha
            var parts = body.Split('&');
            foreach (var p in parts)
            {
                var kv = p.Split('=');
                if (kv.Length == 2 && kv[0].Trim().Equals("team", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1].Trim().Replace('+', ' '));
                }
            }

            return body.Trim();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        public static string GetLocalIpAddress()
        {
            string hotspotCandidate = null, physicalCandidate = null;

            // 1. Scan network interfaces (prioritize 192.168.137.1 and Wi-Fi Direct / Hotspots)
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;

                    string desc = (nic.Description ?? string.Empty).ToLowerInvariant();
                    string name = (nic.Name ?? string.Empty).ToLowerInvariant();
                    bool isHotspot = desc.Contains("wi-fi direct") || desc.Contains("hotspot") ||
                                     desc.Contains("microsoft wi-fi direct virtual adapter") ||
                                     name.Contains("wi-fi direct") || name.Contains("hotspot");

                    bool isVirtualSwitch = (desc.Contains("hyper-v") || desc.Contains("wsl") ||
                                            name.Contains("vethernet") || name.Contains("virtualbox") ||
                                            name.Contains("vmware") ||
                                            ((desc.Contains("virtual") || name.Contains("virtual")) && !isHotspot));

                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(addr.Address)) continue;
                        string ip = addr.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue;
                        if (ip == "192.168.137.1") return ip; // Windows default Mobile Hotspot IP
                        if (isHotspot && hotspotCandidate == null) hotspotCandidate = ip;
                        if (!isVirtualSwitch && physicalCandidate == null &&
                            (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                             nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                        {
                            physicalCandidate = ip;
                        }
                    }
                }
            }
            catch { }

            // 2. Kernel routing table query via UDP connect (when connected to external router/gateway)
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address))
                        return ep.Address.ToString();
                }
            }
            catch { }

            if (hotspotCandidate != null) return hotspotCandidate;
            if (physicalCandidate != null) return physicalCandidate;

            // 3. Fallback to DNS Host Addresses
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)))
                {
                    string s = ip.ToString();
                    if (s == "192.168.137.1") return s;
                    if (!s.StartsWith("169.254.") && !s.StartsWith("172.")) return s;
                }
            }
            catch { }

            return "127.0.0.1";
        }

        public static string DetectLocalIp() => GetLocalIpAddress();


        public static BitmapSource GenerateQrBitmap(string text, int scale = 7, int quietZone = 4)
        {
            return QuizQrCodeEncoder.GenerateQrBitmap(text, scale, quietZone);
        }

        public static bool[,] GenerateQrMatrix(string text)
        {
            return QuizQrCodeEncoder.GenerateQrMatrix(text);
        }

        private static string GetMobileHtml()
        {
            return QuizBuzzerWebAssets.GetMobileHtml();
        }
    }
}
