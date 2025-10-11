using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;


namespace GWCGreenpowerApp;

public class LocationManager
{
    public static MainWindow? mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    public static async void SaveLocation(Location location)
    {
        List<Location> locations = await GetLocations();
        List<Location> toRemove = new();
        foreach (Location x in locations)
        {
            if (location.name == x.name)
            {
                toRemove.Add(x);
            }
        }

        foreach (Location loc in toRemove)
        {
            locations.Remove(loc);
        }
        
        locations.Add(location);
        
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GWCGreenpowerApp");

        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(folder, "locations.json");
        try
        {
            await using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, locations);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving location: {ex.Message}");
            await MessageBoxManager
                .GetMessageBoxStandard("Error Saving location", ex.Message, ButtonEnum.Ok)
                .ShowAsync();
        }

        mainWindow?.UpdateMenuLocations();
    }
    
    public async static Task<List<Location>> GetLocations()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GWCGreenpowerApp"
        );
        string filePath = Path.Combine(folder, "locations.json");

        List<Location> locations = new();

        if (File.Exists(filePath))
        {
            await using FileStream stream = File.OpenRead(filePath);
            var result = new List<Location>();
            try
            {
                result = await JsonSerializer.DeserializeAsync<List<Location>>(stream);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error Loading locations", ex.Message, ButtonEnum.Ok)
                    .ShowAsync();
            }

            if (result is not null)
            {
                locations = result;
                return result;
            }
            else
            {
                string temPath = Path.Combine(folder, "locations.json.default");
                File.Copy(@temPath, @filePath, true);
                return await GetLocations();
            }
        }
        else
        {
            string temPath = Path.Combine(folder, "locations.json.default");
            File.Copy(@temPath, @filePath, true);
            return await GetLocations();
        }

    }
    
    /*
    public static Location GetLocationString(string name)
    {
        throw new NotImplementedException();
    }
    
    public static Location GetLocationIndex(int index)
    {
        throw new NotImplementedException();
    } */
}

public class Location
{
    public string name { get; set; }
    public float lat { get; set; }
    public float lon { get; set; }
    public int zoom { get; set; }
    public bool lapInFile { get; set; }
}