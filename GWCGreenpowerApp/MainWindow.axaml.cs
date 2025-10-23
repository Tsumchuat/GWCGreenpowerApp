using CsvHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Path = System.IO.Path;

namespace GWCGreenpowerApp
{
    public partial class MainWindow : Window
    {
        public string workingFile;
        static readonly HttpClient client = new HttpClient();
        public Location location;
        float latitude = 0f;
        float longitude = 0f;
        private int zoom = 17;
        bool lapInFile = false;
        private int minLapDataCount = 30;
        private List<Lap> fileLaps = new List<Lap>();
        private int lapIndex = 0;
        private float xoffset = 640;
        private float yoffset = 640;

        public MainWindow()
        {
            InitializeComponent();

            //ui events setup
            FileButton.Click += OnFileSelect;
            FilePath.TextChanged += OnFilePathChanged;
            LeftButton.Click += OnLeftButton;
            RightButton.Click += OnRightButton;

            UpdateMenuLocations();
        }

        public async void UpdateMenuLocations()
        {
            List<Location> menuLocations = new List<Location>( await LocationManager.GetLocations());

            menuLocations.Add(new Location() { name = "-" });
            menuLocations.Add(new Location() { name = "Custom" });
            menuLocations.Add(new Location() { name = "-" });
            menuLocations.Add(new Location() { name = "Close" });
            
            var flyout = new MenuFlyout();

            foreach (var location in menuLocations)
            {
                var item = new MenuItem { Header = location.name };
                item.Click += OnLocationPicked;
                flyout.Items.Add(item);
            }
            
            AnalyseButton.Flyout = flyout;
        }
        
        private async void OnLocationPicked(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                string locationName = item.Header.ToString() ?? "";
                if (locationName == "Close" || locationName == "-")
                {
                    return;
                }
                if (locationName == "Custom")
                {
                    CustomLocation();
                }
                else
                {
                    var locations = await LocationManager.GetLocations();
                    var selectedLocation = locations.Find(loc => loc.name == locationName);
                    if (selectedLocation != null)
                    {
                        Analyse(selectedLocation.lat, selectedLocation.lon, selectedLocation.zoom, selectedLocation.lapInFile);
                        location = selectedLocation;
                    }
                }
            }
        }

        private void OnRightButton(object? sender, RoutedEventArgs e)
        {
            if (lapIndex < fileLaps.Count-1)
            {
                lapIndex++;
                DisplayLap(fileLaps[lapIndex], lapIndex+1);
            }
        }

        private void OnLeftButton(object? sender, RoutedEventArgs e)
        {
            if (lapIndex > 0)
            {
                lapIndex--;
                DisplayLap(fileLaps[lapIndex], lapIndex+1);
            }
        }

        async void OnFileSelect(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                AllowMultiple = false,
                Title = "File Picker"
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                workingFile = result[0];
                FilePath.Text = workingFile;
                Debug.WriteLine("Selected file: " + workingFile);
            }
        }

        private void OnFilePathChanged(object? sender, TextChangedEventArgs e)
        {
            workingFile = FilePath.Text ?? "";
        }
        
        static async Task GenerateMap(float lat, float longi, int zooms)
        {
            string url = $"https://maps.googleapis.com/maps/api/staticmap?center={lat},{longi}&zoom={zooms}&size=640x640&maptype=satellite&scale=2&key=AIzaSyBTL-v9AZuWE66IECeZfcsbU07AAG-1FYc";
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync();
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GWCGreenpowerApp", "GWCGreenpowermap.png");
            await File.WriteAllBytesAsync(filePath, data);
        }
        
        static float MapSize(int zoom)
        {
            return 256f * (float)Math.Pow(2, zoom);
        }

        static PointF LatLonToWorld(float lat, float lon, int zoom)
        {
            float latRad = lat * MathF.PI / 180f;
            float mapSize = MapSize(zoom);

            float x = (lon + 180f) / 360f * mapSize;
            float y = (1f - MathF.Log(MathF.Tan(latRad) + 1f / MathF.Cos(latRad)) / MathF.PI) / 2f * mapSize;

            return new PointF(x, y);
        }

        static PointF LatLonToPixel(float? latn, float? lonn,
            float centerLat, float centerLon,
            int zoom)
        {
            if(latn == null || lonn == null)
            {
                return  new PointF(centerLat, centerLon);
            }
            float lat = (float)latn!;
            float lon = (float)lonn!;
            
            var point = LatLonToWorld(lat, lon, zoom);
            var center = LatLonToWorld(centerLat, centerLon, zoom);

            float px = (point.X - center.X) * 2 ; //   *2 for scale=2
            float py = (point.Y - center.Y) * 2 ;

            return new PointF(px, py);
        }
            
        public List<Lap> FindLaps(List<FileData> records)
        {
            List<Lap> laps = new List<Lap>();

            if (!location.lapInFile)
            {
                bool midLap = false;
                Lap currentLap = new Lap();
                foreach (FileData record in records)
                {
                    if (midLap && record.BluetoothConnected != "1")
                    {
                        midLap = false;
                        laps.Add(currentLap);
                        currentLap = new Lap();
                    }
                    else if (midLap)
                    {
                        currentLap.Data.Add(record);
                    }
                    else if (!midLap && record.BluetoothConnected == "1")
                    {
                        midLap = true;
                        currentLap.Data.Add(record);
                    }

                    if (midLap && records.Last() == record)
                    {
                        midLap = false;
                        laps.Add(currentLap);
                        currentLap = new Lap();
                    }
                }
            }
            else
            {
                int lastLapNumber = 0;
                Lap currentLap = new Lap();
                foreach (FileData record in records)
                {
                    if (record.FileLap > lastLapNumber)
                    {
                        laps.Add(currentLap);
                        currentLap = new Lap();
                    }
                    lastLapNumber = record.FileLap ?? 0;
                    currentLap.Data.Add(record);
                }
            }
            

            List<Lap> lapsToRemove = new List<Lap>();
            foreach (Lap lap in laps)
            {
                if (lap.Data.Count <= minLapDataCount)
                {
                    lapsToRemove.Add(lap);
                    continue;
                }
                
                int gpsMoved = 0;
                float lastLat = 0f;
                float lastLon = 0f;
                string startTime = "";
                bool startedLap = false;
                foreach (FileData record in lap.Data)
                {
                    if (lastLat != record.Latitude || lastLon != record.Longitude)
                    {
                        gpsMoved++;
                    }

                    lastLat = record.Latitude ?? 0f;
                    lastLon = record.Longitude ?? 0f;
                    if (!startedLap && record.Current > 10)
                    {
                        startedLap = true;
                        startTime = record.DateTime ?? "";
                    }
                }
                
                if (gpsMoved < 20)
                {
                    lapsToRemove.Add(lap);
                    continue;
                }

                lap.StartTime = startTime;
                lap.MaxRPM = lap.Data.Max(d => d.RPM ?? 0);
                lap.MaxSpeed = lap.Data.Max(d => d.Speed ?? 0);
                lap.MaxCurrent = lap.Data.Max(d => d.Current ?? 0);
                var filtered = lap.Data.Where(n => n.Current.HasValue && n.Current.Value != 0);
                lap.AverageCurrent = filtered.Any() 
                    ? MathF.Round(filtered.Average(n => n.Current.Value), 2) : 0f;


                //   TODO  lap.StartVolt not implemented


            }

            foreach (Lap lap in lapsToRemove)
            {
                laps.Remove(lap);
            }
            
            return laps;
        }

        void DisplayLap(Lap lap, int lapNum)
        {
            OverlayCanvas.Children.Clear();
            foreach (FileData record in lap.Data)
            {
                var point = LatLonToPixel(
                    record.Latitude,
                    record.Longitude,
                    latitude,
                    longitude,
                    zoom);

                var marker = new Ellipse
                {
                    Width = 2,
                    Height = 2,
                    Fill = Brushes.Red
                };                 
                
                Canvas.SetLeft(marker, point.X - 1 + xoffset);
                Canvas.SetTop(marker, point.Y - 1 + yoffset);
                OverlayCanvas.Children.Add(marker);
            }

            LapNum.Text = "Lap Number: " + lapNum;
            LapStart.Text = "Start Time: " +  lap.StartTime;
            LapTime.Text = "Lap Time: ICBA";
            MaxRPM.Text = "Max RPM: " + lap.MaxRPM;
            MaxSpeed.Text = "Max Speed: " + lap.MaxSpeed;
            StartVolt.Text = "Starting Volt: " + lap.StartVolt; //TODO calculate the starting voltage so voltage drop can also be calculated
            VoltDrop.Text = "Volt Drop: " + lap.VoltDrop;
            MaxCurrent.Text = "Max Current: " + lap.MaxCurrent;
            AvereageCurrent.Text = "Avg Current: " + lap.AverageCurrent;

        }

        
        
        private async void CustomLocation()
        {
            var customWindow = new Custom(this);
            await customWindow.ShowDialog(this);
        }

        public async void Analyse(float _lat, float _lon, int _zoom, bool _lapInFile = false)
        {
            latitude = _lat;
            longitude = _lon;
            zoom = _zoom;
            lapInFile = _lapInFile;
            if (string.IsNullOrEmpty(workingFile))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("No File Selected", "Please select a file first.", ButtonEnum.Ok)
                    .ShowAsync();
                return;
            }
            else if (workingFile.EndsWith("Pick a file using the picker"))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("No File Selected", "Please select a file first.", ButtonEnum.Ok)
                    .ShowAsync();
                return;
            }

            var records = new List<FileData>();
            try
            {
                using var reader = new StreamReader(workingFile);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                records = csv.GetRecords<FileData>().ToList();
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error", $"File processing failed: {ex.Message}", ButtonEnum.Ok)
                    .ShowAsync();
                return;
            }

            await GenerateMap(latitude, longitude, zoom);
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GWCGreenpowerApp", "GWCGreenpowermap.png");

            MapImage.Source = new Bitmap(filePath);
            if (!File.Exists(filePath))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error", "Map image not found, please restart the app.", ButtonEnum.Ok)
                    .ShowAsync();
                return;
            }
            
            fileLaps = FindLaps(records);
            lapIndex = 0;
            DisplayLap(fileLaps[lapIndex], lapIndex+1);
        }
        
        private void AddDynamicMenuItem(string name, EventHandler<RoutedEventArgs> onClick)
        {
            
        }
    }

    public class FileData
    {
        public string? BluetoothConnected { get; set; }
        [CsvHelper.Configuration.Attributes.Name("DateTime (date)")]
        public string? DateTime { get; set; }
        [CsvHelper.Configuration.Attributes.Name("GPS latitude (°)")]
        public float? Latitude { get; set; }
        [CsvHelper.Configuration.Attributes.Name("GPS longitude (°)")]
        public float? Longitude { get; set; }
        [CsvHelper.Configuration.Attributes.Name("Current (A)")]
        public float? Current { get; set; }
        [CsvHelper.Configuration.Attributes.Name("Motor speed (RPM)")]
        public int? RPM { get; set; }
        [CsvHelper.Configuration.Attributes.Name("Speed (mph)")]
        public float? Speed { get; set; }  
        [CsvHelper.Configuration.Attributes.Name("Lap")]
        public int? FileLap { get; set; }
    }

    public class Lap
    {
        //TODO calculate lap statistics dynamicaly for better code but a variable isnt that bad forever like just leave it why bother cause i know later you is fucking lazy
        public List<FileData> Data { get; set; } = new List<FileData>();
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public float MaxSpeed { get; set; }
        public int MaxRPM { get; set; }
        public float MaxCurrent { get; set; }
        public float StartVolt { get; set; }
        public float VoltDrop { get; set; }
        public float AverageCurrent { get; set; }
    }
    

}
