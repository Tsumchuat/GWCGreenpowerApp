using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace GWCGreenpowerApp
{
    public static class ResultsMan
{
    public static async Task<List<ResultEntry>> GetResultsAsync(string url) //TODO save to a file if allreaady fetched once to save the http requests and stop getting banned from resultsman
    {
        var results = new List<ResultEntry>();

        results = await GetResultsFromFile(url);
        if (results.Count > 0)
        {
            return results;
        }

        results = new List<ResultEntry>();
        
        var web = new HtmlWeb();
        HtmlDocument page = null;
        try
        {
            page = web.Load(url);
        }
        catch (Exception e)
        {
            await MessageBoxManager
                .GetMessageBoxStandard("Error", $"Could not parse resultsman: {e.Message}", ButtonEnum.Ok)
                .ShowAsync();
            return results;
        }
        
        var table = page.DocumentNode.QuerySelectorAll("table.sortable");
        if (table == null)return results;

        var rows = table.QuerySelectorAll("tr");
        
        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Count < 4)
                continue;

            // Example: column layout [Pos, Time, Name, Car, LapTime]
            var time = cells[1].InnerText.Trim();
            var nameCell = cells[2];
            var name = nameCell.InnerText.Trim();

            // Extract link safely
            var link = nameCell.QuerySelector("a")?.GetAttributeValue("href", "")?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(link) && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                link = "http://resultsman.co.uk/" + link.TrimStart('/');
            link = System.Net.WebUtility.HtmlDecode(link);

            var car = cells[3].InnerText.Trim();
            var lapTimeText = cells[4].InnerText.Trim();

            if (!float.TryParse(lapTimeText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lapTime))
                continue;

            results.Add(new ResultEntry{
                Name = name,
                Car = car,
                LapTime = lapTime,
                StartTime = await GetStartTimeAsync(new HttpClient(), link, lapTime)
                });
        }

        if (results.Count > 0)
        {
            SaveResults(url, results);
        }
        
        return results;
    }

    private static async Task<string> GetStartTimeAsync(HttpClient client, string competitorUrl, float lapTime)
    {
        try
        {
            const int maxRetries = 2;
            int retries = 0;
        
            while (true)
            {
                try
                {
                    var html = await client.GetStringAsync(competitorUrl);
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);
        
                    // Find all lap divs that have both a time (HH:mm) and a float number
                    var a = doc.DocumentNode.SelectNodes("//div[contains(@class,'bg1')]");
                    var b = doc.DocumentNode.SelectNodes("//div[contains(@class,'bg2')]");
                    var lapDivs = (a ?? Enumerable.Empty<HtmlNode>()).Concat(b ?? Enumerable.Empty<HtmlNode>());
        
                    foreach (var lap in lapDivs)
                    {
                        var timeNode = lap.SelectSingleNode(".//div[contains(@style,'left:10px')]");
                        var lapTimeNode = lap.SelectSingleNode(".//div[contains(@style,'left:50%')]");
        
                        if (timeNode == null || lapTimeNode == null)
                            continue;
        
                        var startTime = timeNode.InnerText.Trim();
                        var lapText = lapTimeNode.InnerText.Trim();
        
                        if (float.TryParse(lapText, out var lapVal))
                        {
                            // match times with a tolerance
                            if (Math.Abs(lapVal - lapTime) < 0.05f)
                                return startTime;
                        }
                    }
        
                    break; // success, exit the retry loop
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    retries++;
                    Console.WriteLine($"Error 500 fetching {competitorUrl}, retry {retries}/{maxRetries}...");
        
                    if (retries > maxRetries)
                    {
                        Console.WriteLine($"Giving up on {competitorUrl} after {maxRetries + 1} attempts.");
                        break;
                    }
        
                    await Task.Delay(1000); // optional small delay before retry
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error fetching {competitorUrl}: {ex.Message}");
                    break; // don't retry non-HTTP 500 errors
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in fetch routine: {ex.Message}");
        }


        return "";
    }
    
    public static async void SaveResults(string url, List<ResultEntry> results)
    {
        List<SavedResult> savedResults = await GetAllResultsFromFile(); 
        
        SavedResult newResult = new SavedResult
        {
            url = url,
            results = results,
        };
        
        savedResults.Add(newResult);
        
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GWCGreenpowerApp");

        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(folder, "results.json");
        try
        {
            await using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, savedResults);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving results: {ex.Message}");
            await MessageBoxManager
                .GetMessageBoxStandard("Error Saving results for future use", ex.Message, ButtonEnum.Ok)
                .ShowAsync();
        }
    }
    public async static Task<List<ResultEntry>>? GetResultsFromFile(string url)
    {
        List<SavedResult> results = await  GetAllResultsFromFile();
        
        foreach (SavedResult result in results)
        {
            if (result.url == url)
            {
                return result.results;
            }
        }

        return new List<ResultEntry>();
    }

    public async static Task<List<SavedResult>> GetAllResultsFromFile()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GWCGreenpowerApp"
        );
        string filePath = Path.Combine(folder, "results.json");

        List<SavedResult> results = new();

        if (File.Exists(filePath))
        {
            await using FileStream stream = File.OpenRead(filePath);
            try
            {
                results = await JsonSerializer.DeserializeAsync<List<SavedResult>>(stream);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error Loading results from file", ex.Message, ButtonEnum.Ok)
                    .ShowAsync();
            }

            if (results is not null)
            {
                return results;
            }
            else
            {
                return new List<SavedResult>();
            }
        }
        else
        {
            return new List<SavedResult>();
        }
    }
}

}
