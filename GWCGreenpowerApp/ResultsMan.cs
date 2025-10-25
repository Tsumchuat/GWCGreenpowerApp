using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace GWCGreenpowerApp
{
    public class ResultEntry
    {
        public string Name { get; set; } = "";
        public string Car { get; set; } = "";
        public float LapTime { get; set; } = 0f;
        public  string StartTime { get; set; } = "";
    }

    public static class ResultsMan
{
    public static async Task<List<ResultEntry>> GetResultsAsync(string url)
    {
        var results = new List<ResultEntry>();
        
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

        return results;
    }

    private static async Task<string> GetStartTimeAsync(HttpClient client, string competitorUrl, float lapTime)
    {
        try
        {
            var html = await client.GetStringAsync(competitorUrl);
            var doc = new HtmlDocument();
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching {competitorUrl}: {ex.Message}");
        }

        return "";
    }
}

}
