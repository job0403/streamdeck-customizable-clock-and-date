using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static string port;
    private static string pluginUUID;
    private static string registerEvent;
    private static ClientWebSocket webSocket;
    private static readonly Dictionary<string, ContextSettings> activeContexts = new Dictionary<string, ContextSettings>();
    private static PrivateFontCollection customFonts = new PrivateFontCollection();

    class ContextSettings
    {
        public string TimeFormat { get; set; }
        public string DateFormat { get; set; }

        public string TimeFontFamily { get; set; }
        public int TimeFontWeight { get; set; }
        public string TimeColor { get; set; }
        public int TimeSize { get; set; }
        public float TimeStretch { get; set; }
        public float TimeLetterSpacing { get; set; }
        public int TimeY { get; set; }

        public string SecFontFamily { get; set; }
        public int SecFontWeight { get; set; }
        public string SecColor { get; set; }
        public int SecSize { get; set; }
        public float SecStretch { get; set; }
        public float SecLetterSpacing { get; set; }
        public int SecY { get; set; }

        public string DateFontFamily { get; set; }
        public int DateFontWeight { get; set; }
        public string DateColor { get; set; }
        public int DateSize { get; set; }
        public float DateLetterSpacing { get; set; }
        public int DateY { get; set; }

        public string LastTimeString { get; set; }
        public string CachedDataURL { get; set; }

        public ContextSettings()
        {
            TimeFormat = "24h";
            DateFormat = "SAT 6 JUN";

            TimeFontFamily = "SF Pro Display";
            TimeFontWeight = 600;
            TimeColor = "#ffffff";
            TimeSize = 55;
            TimeStretch = 0.85f;
            TimeLetterSpacing = 0.0f;
            TimeY = 65;

            SecFontFamily = "SF Pro Display";
            SecFontWeight = 500;
            SecColor = "#ffffff";
            SecSize = 26;
            SecStretch = 1.0f;
            SecLetterSpacing = 3.0f;
            SecY = 109;

            DateFontFamily = "SF Pro Display";
            DateFontWeight = 600;
            DateColor = "#ffffff";
            DateSize = 24;
            DateLetterSpacing = 0.0f;
            DateY = 13;

            LastTimeString = "";
            CachedDataURL = "";
        }
    }

    static void Main(string[] args)
    {
        MainAsync(args).GetAwaiter().GetResult();
    }

    private static async Task MainAsync(string[] args)
    {
        // Stream Deck passes arguments in pairs: -port <port> -pluginUUID <uuid> -registerEvent <registerEvent>
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-port" && i + 1 < args.Length) port = args[++i];
            else if (args[i] == "-pluginUUID" && i + 1 < args.Length) pluginUUID = args[++i];
            else if (args[i] == "-registerEvent" && i + 1 < args.Length) registerEvent = args[++i];
        }

        if (string.IsNullOrEmpty(port) || string.IsNullOrEmpty(pluginUUID) || string.IsNullOrEmpty(registerEvent))
        {
            return;
        }

        try
        {
            // Load custom bundled fonts
            string fontsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts");
            if (Directory.Exists(fontsDir))
            {
                foreach (var file in Directory.GetFiles(fontsDir))
                {
                    if (file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    {
                        try { customFonts.AddFontFile(file); } catch { }
                    }
                }
            }

            // Connect to Stream Deck WebSocket server
            webSocket = new ClientWebSocket();
            Uri uri = new Uri("ws://127.0.0.1:" + port);
            await webSocket.ConnectAsync(uri, CancellationToken.None);

            // Register the plugin
            string registerJson = string.Format("{{\"event\":\"{0}\",\"uuid\":\"{1}\"}}", registerEvent, pluginUUID);
            byte[] registerBytes = Encoding.UTF8.GetBytes(registerJson);
            await webSocket.SendAsync(new ArraySegment<byte>(registerBytes), WebSocketMessageType.Text, true, CancellationToken.None);

            // Start message receiving loop
            Task.Run(new Func<Task>(ReceiveLoop));

            // Start tick loop at 250ms intervals
            while (webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(250);
                await TickClock();
            }
        }
        catch (Exception)
        {
            // Exit gracefully on errors or socket closures
        }
    }

    private static async Task ReceiveLoop()
    {
        byte[] buffer = new byte[1024 * 8];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ParseMessage(message);
                }
            }
        }
        catch (Exception)
        {
            // Exit loop on network disruptions
        }
    }

    private static void ParseMessage(string message)
    {
        string context = ExtractJsonField(message, "context");
        if (string.IsNullOrEmpty(context)) return;

        if (message.Contains("\"willAppear\"") || message.Contains("\"didReceiveSettings\""))
        {
            ContextSettings settings = new ContextSettings();
            
            settings.TimeFormat = ParseStringField(message, "timeFormat", "24h");
            settings.DateFormat = ParseStringField(message, "dateFormat", "SAT 6 JUN");

            settings.TimeFontFamily = ParseStringField(message, "timeFontFamily", "SF Pro Display");
            settings.TimeFontWeight = ParseIntField(message, "timeFontWeight", 600);
            settings.TimeColor = ParseStringField(message, "timeColor", "#ffffff");
            settings.TimeSize = ParseIntField(message, "timeSize", 55);
            settings.TimeStretch = ParseFloatField(message, "timeStretch", 0.85f);
            settings.TimeLetterSpacing = ParseFloatField(message, "timeLetterSpacing", 0.0f);
            settings.TimeY = ParseIntField(message, "timeY", 65);

            settings.SecFontFamily = ParseStringField(message, "secFontFamily", "SF Pro Display");
            settings.SecFontWeight = ParseIntField(message, "secFontWeight", 500);
            settings.SecColor = ParseStringField(message, "secColor", "#ffffff");
            settings.SecSize = ParseIntField(message, "secSize", 26);
            settings.SecStretch = ParseFloatField(message, "secStretch", 1.0f);
            settings.SecLetterSpacing = ParseFloatField(message, "secLetterSpacing", 3.0f);
            settings.SecY = ParseIntField(message, "secY", 109);

            settings.DateFontFamily = ParseStringField(message, "dateFontFamily", "SF Pro Display");
            settings.DateFontWeight = ParseIntField(message, "dateFontWeight", 600);
            settings.DateColor = ParseStringField(message, "dateColor", "#ffffff");
            settings.DateSize = ParseIntField(message, "dateSize", 24);
            settings.DateLetterSpacing = ParseFloatField(message, "dateLetterSpacing", 0.0f);
            settings.DateY = ParseIntField(message, "dateY", 13);

            UpdateContextSettings(context, settings);
            Task.Run(async () => await ForceDraw(context));
        }
        else if (message.Contains("\"willDisappear\""))
        {
            lock (activeContexts)
            {
                activeContexts.Remove(context);
            }
        }
    }

    private static void UpdateContextSettings(string context, ContextSettings settings)
    {
        lock (activeContexts)
        {
            activeContexts[context] = settings;
        }
    }

    private static async Task TickClock()
    {
        KeyValuePair<string, ContextSettings>[] contexts;
        lock (activeContexts)
        {
            if (activeContexts.Count == 0) return;
            contexts = new KeyValuePair<string, ContextSettings>[activeContexts.Count];
            int i = 0;
            foreach (var kvp in activeContexts)
            {
                contexts[i++] = kvp;
            }
        }

        DateTime now = DateTime.Now;

        foreach (var kvp in contexts)
        {
            string context = kvp.Key;
            ContextSettings settings = kvp.Value;

            string dateStr, timeMainStr, timeSecsStr;
            GetClockStrings(now, settings, out dateStr, out timeMainStr, out timeSecsStr);

            string timeString = string.Format("{0}|{1}|{2}", dateStr, timeMainStr, timeSecsStr);

            if (timeString != settings.LastTimeString || string.IsNullOrEmpty(settings.CachedDataURL))
            {
                settings.LastTimeString = timeString;
                settings.CachedDataURL = GenerateClockImage(dateStr, timeMainStr, timeSecsStr, settings);
                await SendImage(context, settings.CachedDataURL);
            }
        }
    }

    private static async Task ForceDraw(string context)
    {
        ContextSettings settings;
        lock (activeContexts)
        {
            if (!activeContexts.TryGetValue(context, out settings)) return;
        }

        DateTime now = DateTime.Now;
        string dateStr, timeMainStr, timeSecsStr;
        GetClockStrings(now, settings, out dateStr, out timeMainStr, out timeSecsStr);

        settings.LastTimeString = string.Format("{0}|{1}|{2}", dateStr, timeMainStr, timeSecsStr);
        settings.CachedDataURL = GenerateClockImage(dateStr, timeMainStr, timeSecsStr, settings);
        await SendImage(context, settings.CachedDataURL);
    }

    private static void GetClockStrings(DateTime now, ContextSettings settings, out string dateStr, out string timeMainStr, out string timeSecsStr)
    {
        // 1. Format Time
        string timeFormatPattern = settings.TimeFormat == "12h" ? "hh:mm" : "HH:mm";
        timeMainStr = now.ToString(timeFormatPattern);
        if (settings.TimeFormat == "12h")
        {
            timeMainStr = now.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        }

        // 2. Format Seconds
        timeSecsStr = now.ToString("ss");

        // 3. Format Date
        if (settings.DateFormat == "06 JUN 2026")
        {
            string[] months = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            dateStr = string.Format("{0:00} {1} {2}", now.Day, months[now.Month - 1], now.Year);
        }
        else // default "SAT 6 JUN"
        {
            string[] days = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };
            string[] months = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            dateStr = string.Format("{0} {1} {2}", days[(int)now.DayOfWeek], now.Day, months[now.Month - 1]);
        }
    }

    private static async Task SendImage(string context, string base64Image)
    {
        if (webSocket.State != WebSocketState.Open) return;

        string payload = string.Format(
            "{{\r\n  \"event\": \"setImage\",\r\n  \"context\": \"{0}\",\r\n  \"payload\": {{\r\n    \"image\": \"{1}\",\r\n    \"target\": 0\r\n  }}\r\n}}",
            context, base64Image
        );

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        await webSocket.SendAsync(new ArraySegment<byte>(payloadBytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static Font CreateFont(string familyName, float size, int weight)
    {
        List<string> candidates = new List<string>();
        FontStyle style = FontStyle.Regular;

        if (weight >= 600)
        {
            style = FontStyle.Bold;
        }

        // Add weight-specific family name variants first
        if (weight == 100)
        {
            candidates.Add(familyName + " Thin");
            candidates.Add(familyName + " Light");
        }
        else if (weight == 200)
        {
            candidates.Add(familyName + " ExtraLight");
            candidates.Add(familyName + " UltraLight");
            candidates.Add(familyName + " Light");
        }
        else if (weight == 300)
        {
            candidates.Add(familyName + " Light");
        }
        else if (weight == 500)
        {
            candidates.Add(familyName + " Medium");
        }
        else if (weight == 600)
        {
            candidates.Add(familyName + " SemiBold");
            candidates.Add(familyName + " Semibold");
        }
        else if (weight == 800)
        {
            candidates.Add(familyName + " ExtraBold");
            candidates.Add(familyName + " Heavy");
            candidates.Add(familyName + " Black");
        }
        else if (weight == 900)
        {
            candidates.Add(familyName + " Black");
            candidates.Add(familyName + " Heavy");
        }

        // Add the base family name
        candidates.Add(familyName);

        // Standard fallback stack
        string[] fallbacks = { "Segoe UI", "Arial" };
        foreach (var f in fallbacks)
        {
            if (!candidates.Contains(f)) candidates.Add(f);
        }

        foreach (var name in candidates)
        {
            // First check custom bundled fonts
            foreach (var fam in customFonts.Families)
            {
                if (string.Equals(fam.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return new Font(fam, size, style, GraphicsUnit.Pixel);
                    }
                    catch { }
                }
            }

            // Fallback to system fonts
            try
            {
                Font font = new Font(name, size, style, GraphicsUnit.Pixel);
                if (font.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }
                font.Dispose();
            }
            catch { }
        }

        return new Font(SystemFonts.DefaultFont.FontFamily, size, style, GraphicsUnit.Pixel);
    }

    private static void DrawStringWithSpacing(Graphics g, string text, Font font, Brush brush, float centerX, float y, float letterSpacing, StringFormat format)
    {
        if (letterSpacing == 0)
        {
            g.DrawString(text, font, brush, centerX, y, format);
            return;
        }

        // Measure individual characters using the typographic string format
        float totalWidth = 0;
        float[] charWidths = new float[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            SizeF size = g.MeasureString(text[i].ToString(), font, 144, StringFormat.GenericTypographic);
            charWidths[i] = size.Width;
            totalWidth += size.Width;
            if (i < text.Length - 1) totalWidth += letterSpacing;
        }

        // Draw character-by-character starting from the centered alignment offset
        float currentX = centerX - totalWidth / 2;
        for (int i = 0; i < text.Length; i++)
        {
            // Position the center of each character correctly
            g.DrawString(text[i].ToString(), font, brush, currentX + charWidths[i] / 2, y, format);
            currentX += charWidths[i] + letterSpacing;
        }
    }

    private static string GenerateClockImage(string dateStr, string timeMainStr, string timeSecsStr, ContextSettings settings)
    {
        using (Bitmap bmp = new Bitmap(144, 144))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Center alignment formats
                using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;

                    // 1. Draw Date
                    using (Font dateFont = CreateFont(settings.DateFontFamily, settings.DateSize, settings.DateFontWeight))
                    using (Brush dateBrush = new SolidBrush(ColorTranslator.FromHtml(settings.DateColor)))
                    {
                        DrawStringWithSpacing(g, dateStr, dateFont, dateBrush, 72, settings.DateY, settings.DateLetterSpacing, format);
                    }

                    // 2. Draw Time (HH:MM)
                    GraphicsState timeState = g.Save();
                    g.TranslateTransform(72, settings.TimeY);
                    g.ScaleTransform(settings.TimeStretch, 1.0f);
                    using (Font timeFont = CreateFont(settings.TimeFontFamily, settings.TimeSize, settings.TimeFontWeight))
                    using (Brush timeBrush = new SolidBrush(ColorTranslator.FromHtml(settings.TimeColor)))
                    {
                        DrawStringWithSpacing(g, timeMainStr, timeFont, timeBrush, 0, 0, settings.TimeLetterSpacing, format);
                    }
                    g.Restore(timeState);

                    // 3. Draw Seconds
                    GraphicsState secState = g.Save();
                    g.TranslateTransform(72, settings.SecY);
                    g.ScaleTransform(settings.SecStretch, 1.0f);
                    using (Font secFont = CreateFont(settings.SecFontFamily, settings.SecSize, settings.SecFontWeight))
                    using (Brush secBrush = new SolidBrush(ColorTranslator.FromHtml(settings.SecColor)))
                    {
                        DrawStringWithSpacing(g, timeSecsStr, secFont, secBrush, 0, 0, settings.SecLetterSpacing, format);
                    }
                    g.Restore(secState);
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                byte[] byteImage = ms.ToArray();
                return "data:image/png;base64," + Convert.ToBase64String(byteImage);
            }
        }
    }

    private static string ExtractJsonField(string json, string fieldName)
    {
        string search = "\"" + fieldName + "\":\"";
        int idx = json.IndexOf(search);
        if (idx == -1)
        {
            search = "\"" + fieldName + "\":";
            idx = json.IndexOf(search);
            if (idx == -1) return null;
            int startIdx = idx + search.Length;
            int endIdx = json.IndexOf(",", startIdx);
            if (endIdx == -1) endIdx = json.IndexOf("}", startIdx);
            if (endIdx == -1) return null;
            return json.Substring(startIdx, endIdx - startIdx).Trim('\"', ' ', '\r', '\n');
        }
        else
        {
            int startIdx = idx + search.Length;
            int endIdx = json.IndexOf("\"", startIdx);
            if (endIdx == -1) return null;
            return json.Substring(startIdx, endIdx - startIdx);
        }
    }

    private static string ParseStringField(string json, string fieldName, string defaultValue)
    {
        string val = ExtractJsonField(json, fieldName);
        return string.IsNullOrEmpty(val) ? defaultValue : val;
    }

    private static int ParseIntField(string json, string fieldName, int defaultValue)
    {
        string valStr = ExtractJsonField(json, fieldName);
        if (string.IsNullOrEmpty(valStr)) return defaultValue;
        int result;
        if (int.TryParse(valStr, out result)) return result;
        return defaultValue;
    }

    private static float ParseFloatField(string json, string fieldName, float defaultValue)
    {
        string valStr = ExtractJsonField(json, fieldName);
        if (string.IsNullOrEmpty(valStr)) return defaultValue;
        float result;
        if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result)) return result;
        return defaultValue;
    }
}
