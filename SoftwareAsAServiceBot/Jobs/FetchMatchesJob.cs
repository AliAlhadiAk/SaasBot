using Quartz;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SoftwareAsAServiceBot.Caching; // Ensure Newtonsoft.Json package is installed

public class FetchMatchesJob : IJob
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey = "231bef8d4fc14fc4a84f2e43d5e7f034";
    private readonly string _telegramBotToken = "6948749472:AAGMHmuPxmpTWmeGdy9zuQiRRyU0vanKMgU";
    private readonly string _chatId = "5111371354";
    private readonly ICacheService _cacheService;

    public FetchMatchesJob(IHttpClientFactory httpClientFactory, ICacheService cacheService)
    {
        _httpClientFactory = httpClientFactory;
        _cacheService = cacheService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

            var cacheKey = $"matches_{today}_{tomorrow}";
            var cachedMatchesResponse = await _cacheService.GetData<string>(cacheKey);

            string matchesResponse;

            if (string.IsNullOrEmpty(cachedMatchesResponse))
            {
                var matchesUrl = $"https://api.football-data.org/v4/matches?dateFrom={today}&dateTo={tomorrow}";
                matchesResponse = await FetchMatchesAsync(matchesUrl);

                if (!string.IsNullOrEmpty(matchesResponse))
                {
                    await _cacheService.SetData(cacheKey, matchesResponse, TimeSpan.FromHours(5));
                }
            }
            else
            {
                matchesResponse = cachedMatchesResponse;
            }

            if (!string.IsNullOrEmpty(matchesResponse))
            {
                var formattedMessage = FormatMatches(matchesResponse);

                if (!string.IsNullOrEmpty(formattedMessage))
                {
                    var messages = SplitMessage(formattedMessage);
                    foreach (var message in messages)
                    {
                        await SendMessageToTelegramAsync(_telegramBotToken, _chatId, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FetchMatchesJob: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task<string> FetchMatchesAsync(string url)
    {
        try
        {
            using (var client = _httpClientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-Auth-Token", _apiKey);
                return await client.GetStringAsync(url);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch matches data: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task SendMessageToTelegramAsync(string botToken, string chatId, string message)
    {
        var telegramUrl = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";

        try
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(telegramUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Telegram API Response: {responseContent}");

                response.EnsureSuccessStatusCode();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send message to Telegram: {ex.Message}");
        }
    }

    private IEnumerable<string> SplitMessage(string message, int chunkSize = 4096)
    {
        for (int i = 0; i < message.Length; i += chunkSize)
        {
            yield return message.Substring(i, Math.Min(chunkSize, message.Length - i));
        }
    }

    private string FormatMatches(string jsonResponse)
    {
        var json = JObject.Parse(jsonResponse);
        var matches = json["matches"];

        if (matches == null || !matches.Any())
        {
            return "No matches found.";
        }

        var formattedMessage = new StringBuilder();

        foreach (var match in matches)
        {
            var competition = match["competition"]?["name"]?.ToString();

            // Filter only La Liga matches
            if (competition != "Primera Division")
            {
                continue;
            }

            var homeTeam = match["homeTeam"]?["name"]?.ToString();
            var awayTeam = match["awayTeam"]?["name"]?.ToString();
            var date = DateTime.Parse(match["utcDate"]?.ToString()).ToString("yyyy-MM-dd HH:mm");
            var status = match["status"]?.ToString();

            var homeTeamCrest = match["homeTeam"]?["crest"]?.ToString();
            var awayTeamCrest = match["awayTeam"]?["crest"]?.ToString();

            var scoreHome = match["score"]?["fullTime"]?["home"]?.ToString() ?? "N/A";
            var scoreAway = match["score"]?["fullTime"]?["away"]?.ToString() ?? "N/A";
            var score = $"{scoreHome} - {scoreAway}";

            // Append formatted match details
            formattedMessage.AppendLine($"📅 Date: {date}");
            formattedMessage.AppendLine($"🏆 Competition: {competition}");
            formattedMessage.AppendLine($"🏟️ Home Team: {homeTeam} {(!string.IsNullOrEmpty(homeTeamCrest) ? $"<a href='{homeTeamCrest}'>🆔</a>" : "")}");
            formattedMessage.AppendLine($"⚽ Away Team: {awayTeam} {(!string.IsNullOrEmpty(awayTeamCrest) ? $"<a href='{awayTeamCrest}'>🆔</a>" : "")}");
            formattedMessage.AppendLine($"🔢 Score: {score}");
            formattedMessage.AppendLine($"📍 Status: {status}");
            formattedMessage.AppendLine();
        }

        return formattedMessage.ToString();
    }
}
