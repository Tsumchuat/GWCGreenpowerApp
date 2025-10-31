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
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Path = System.IO.Path;
using Point = Avalonia.Point;

//TODO add a fancy loading screen but not high priority there is way more important stuff
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
        private float userXOffset = 0;
        private float useryOffset = 0;

        private List<Lap> currentlyDisplayed = new List<Lap>();

        public MainWindow()
        {
            InitializeComponent();

            //ui events setup
            FileButton.Click += OnFileSelect;
            FilePath.TextChanged += OnFilePathChanged;
            LeftButton.Click += OnLeftButton;
            RightButton.Click += OnRightButton;

            xOffsetBox.TextChanged += OnxOffset;
            yOffsetBox.TextChanged += OnyOffset;

            AnalyseTabs.SelectionChanged += OnTabChanged;

            UpdateMenuLocations();
            
        }

        private void OnTabChanged(object? sender, SelectionChangedEventArgs e) //TODO fix changing the offsets just displays the other tabs lap IMPORTANT proably just make sure when changed it redraws currently selected not lapindex
        {
            if (sender is TabControl tab && tab.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Header == "Compare")
                {
                    LeftButton.IsEnabled = false;
                    RightButton.IsEnabled = false;
                    LeftButton.IsVisible = false;
                    RightButton.IsVisible = false;
                    OverlayCanvas.Children.Clear();
                    currentlyDisplayed = new List<Lap>();
                }
                else
                {
                    LeftButton.IsEnabled = true;
                    RightButton.IsEnabled = true;
                    LeftButton.IsVisible = true;
                    RightButton.IsVisible = true;
                    lapIndex = 0;
                    if (fileLaps.Count < 1) return;
                    DisplayLap(fileLaps[lapIndex]);
                }
            }
        }

        private void OnyOffset(object? sender, TextChangedEventArgs e)
        {
            float.TryParse(yOffsetBox.Text, out useryOffset);
            if(fileLaps.Count>0) DisplayLap(fileLaps[lapIndex]);
            
        }

        private void OnxOffset(object? sender, TextChangedEventArgs e)
        {
            float.TryParse(xOffsetBox.Text, out userXOffset);
            if(fileLaps.Count>0) DisplayLap(fileLaps[lapIndex]);
        }

        public async void UpdateMenuLocations()
        {
            List<Location> menuLocations = new List<Location>(await LocationManager.GetLocations());

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
                        Analyse(selectedLocation.lat, selectedLocation.lon, selectedLocation.zoom,
                            selectedLocation.lapInFile);
                        location = selectedLocation;
                    }
                }
            }
        }

        private void OnRightButton(object? sender, RoutedEventArgs e)
        {
            if (lapIndex < fileLaps.Count - 1)
            {
                lapIndex++;
                DisplayLap(fileLaps[lapIndex]);
            }
        }

        private void OnLeftButton(object? sender, RoutedEventArgs e)
        {
            if (lapIndex > 0)
            {
                lapIndex--;
                DisplayLap(fileLaps[lapIndex]);
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
            string url =
                $"https://maps.googleapis.com/maps/api/staticmap?center={lat},{longi}&zoom={zooms}&size=640x640&maptype=satellite&scale=2&key=AIzaSyBTL-v9AZuWE66IECeZfcsbU07AAG-1FYc";
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync();
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GWCGreenpowerApp", "GWCGreenpowermap.png");
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
            if (latn == null || lonn == null)
            {
                return new PointF(centerLat, centerLon);
            }

            float lat = (float)latn!;
            float lon = (float)lonn!;

            var point = LatLonToWorld(lat, lon, zoom);
            var center = LatLonToWorld(centerLat, centerLon, zoom);

            float px = (point.X - center.X) * 2; //   *2 for scale=2
            float py = (point.Y - center.Y) * 2;

            return new PointF(px, py);
        }

        public async Task<List<Lap>> FindLaps(List<FileData> records, string resultsURL)
        {
            bool hasResultsLink = false;
            List<ResultEntry> resultEntries = new List<ResultEntry>();
            if (!String.IsNullOrEmpty(resultsURL))
            {
                LoadingText.Text = "Getting Lap Times... \n (this may take a while)";
                resultEntries = await ResultsMan.GetResultsAsync(resultsURL);
                hasResultsLink = true;
            }

            LoadingText.Text = "Finding Laps...";

            List<Lap> laps = new List<Lap>();

            if (!location.lapInFile) //TODO change this to use the throttle button instead of current when implemented
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
            int x = 0;
            foreach (Lap lap in laps)
            {
                x++;
                if (lap.Data.Count <= minLapDataCount)
                {
                    lapsToRemove.Add(lap);
                    continue;
                }

                int gpsMoved = 0;
                float lastLat = 0f;
                float lastLon = 0f;
                DateTime startTime = default;
                DateTime endTime = default;
                bool startedLap = false;
                bool finishedLap = false;
                
                for (int i = 0; i < lap.Data.Count; i++)
                {
                    var record = lap.Data[i];

                    if (lastLat != record.Latitude || lastLon != record.Longitude)
                    {
                        gpsMoved++;
                    }

                    lastLat = record.Latitude ?? 0f;
                    lastLon = record.Longitude ?? 0f;
                    if (!startedLap && record.Current > 10)
                    {
                        startedLap = true;
                        startTime = ParseDateTime(record.DateTimeString, record.Miliseconds);
                    }

                    if (startedLap && !finishedLap && record.Current < 5 && (i + 20) < lap.Data.Count &&
                        lap.Data[i + 20].Current < 5 && lap.Data[i + 10].Current < 5 && i > ((lap.Data.Count / 10) * 7))
                    {
                        endTime = ParseDateTime(record.DateTimeString, record.Miliseconds);
                        finishedLap = true;
                    }
                }

                if (gpsMoved < 20)
                {
                    lapsToRemove.Add(lap);
                    continue;
                }

                lap.StartTime = startTime;
                lap.EndTime = endTime;
                lap.EstimatedTime =
                    (endTime - startTime) -
                    new TimeSpan(0, 0, 0, 0, 900); //subtract time to account for human delay after crossing the line
                if (location.lapInFile)
                {
                    lap.EstimatedTime =
                        (endTime -
                         startTime); // redo the estimated time without subtracting if they laps are in the file
                    lap.LapTime =
                        Math.Round(lap.EstimatedTime.TotalSeconds, 2)
                            .ToString(); //TODO fetch speedhive for greenpower and match if u cba
                }
                else if (hasResultsLink) //TODO see what happens if no results are found in the timeframe  
                {
                    //writen by ai i dont fully get to sorry future me
                    var filtered = resultEntries
                        .Select(r =>
                        {
                            if (TimeSpan.TryParse(r.StartTime, out var startTimeSpan))
                            {
                                // Combine with the date from lap.StartTime
                                var startDateTime = lap.StartTime.Date + startTimeSpan;
                                return (Result: r, Start: startDateTime);
                            }

                            return (Result: (ResultEntry?)null, Start: DateTime.MinValue);
                        })
                        .Where(x => x.Result != null
                                    && Math.Abs((x.Start - lap.StartTime).TotalMinutes) <= 3)
                        .ToList();

                    if (filtered.Any())
                    {
                        var closest = filtered
                            .OrderBy(x => Math.Abs(x.Result!.LapTime - (float)lap.EstimatedTime.TotalSeconds))
                            .First()
                            .Result!;

                        lap.LapTime = MathF.Round(closest.LapTime, 2).ToString();
                        lap.Driver = closest.Name;
                        lap.Car = closest.Car;
                    }

                }
                else
                {
                    lap.LapTime = Math.Round(lap.EstimatedTime.TotalSeconds, 2).ToString();
                }

                lap.MaxRPM = lap.Data.Max(d => d.RPM ?? 0); //Filter out stupid values maybe by checking the ones around it idk 
                lap.MaxSpeed = lap.Data.Max(d => d.Speed ?? 0);
                lap.MaxCurrent = lap.Data.Max(d => d.Current ?? 0);
                var filteredx = lap.Data.Where(n => n.Current.HasValue && n.Current.Value != 0);
                lap.AverageCurrent = filteredx.Any()
                    ? MathF.Round(filteredx.Average(n => n.Current.Value), 2)
                    : 0f;

                lap.ID = x;
                
                float tempvoltdiv3 = (lap.Data[3].Voltage ?? 0) + (lap.Data[4].Voltage ?? 0) + (lap.Data[5].Voltage ?? 0);
                lap.StartVolt = tempvoltdiv3 / 3;

            }

            foreach (Lap lap in lapsToRemove)
            {
                laps.Remove(lap);
            }

            for (int i = 0; i < laps.Count; i++)
            {
                laps[i].Number = i + 1;
            }

            return laps;
        }

        void DisplayLap(Lap lap, bool additional = false)
        {
            PointF? oldPoint = null;
            if (!additional)
            {
                OverlayCanvas.Children.Clear();
                currentlyDisplayed = new List<Lap>();
                currentlyDisplayed.Add(lap);
            }
            else
            {
                currentlyDisplayed.Add(lap);
            }
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
                    Width = 4,
                    Height = 4,
                    Fill = Brushes.Red
                };

                Canvas.SetLeft(marker, point.X - marker.Width/2 + xoffset + userXOffset);
                Canvas.SetTop(marker, point.Y - marker.Height/2 + yoffset + useryOffset);
                OverlayCanvas.Children.Add(marker);

                if (oldPoint == null)
                {
                    oldPoint = point; 
                    continue;
                } 

                var line = new Line
                {
                    Stroke = Brushes.Green,
                    StrokeThickness = 3,
                    StartPoint = new Avalonia.Point(oldPoint.Value.X, oldPoint.Value.Y),
                    EndPoint = new Avalonia.Point(point.X, point.Y)
                };
        
                Canvas.SetLeft(line, xoffset + userXOffset);
                Canvas.SetTop(line, yoffset + useryOffset);
                OverlayCanvas.Children.Add(line);
                
                oldPoint = point;
            }

            LapNum.Text = "Lap Number: " + lap.Number;
            LapStart.Text = "Start Time: " + new TimeSpan(lap.StartTime.Hour, lap.StartTime.Minute, lap.StartTime.Second);
            LapTime.Text = "Lap Time: " + lap.LapTime + "s";
            DriverName.Text = "Matched Driver: " + lap.Driver;
            CarName.Text = "Matched Car: " + lap.Car;
            MaxRPM.Text = "Max RPM: " + lap.MaxRPM;
            MaxSpeed.Text = "Max Speed: " + lap.MaxSpeed;
            StartVolt.Text = "Starting Volt: " + MathF.Round(lap.StartVolt, 2);
            VoltDrop.Text = "Volt Drop: " + lap.VoltDrop;
            MaxCurrent.Text = "Max Current: " + lap.MaxCurrent;
            AvereageCurrent.Text = "Avg Current: " + lap.AverageCurrent;
            ID.Text = "Lap Debug ID: " + lap.ID;

            AnalyseLoading.IsVisible = false;
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

            var analysePopup = new AnalysePopup();
            await analysePopup.ShowDialog(this);

            string resultsURL = analysePopup.url;
            
            AnalyseLoading.IsVisible = true;
            LoadingText.Text = "Parsing File...";

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

            LoadingText.Text = "Generating Map...";
            
            await GenerateMap(latitude, longitude, zoom);
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GWCGreenpowerApp", "GWCGreenpowermap.png");

            
            if (!File.Exists(filePath))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error", "Map image not found, please restart the app.", ButtonEnum.Ok)
                    .ShowAsync();
                return;
            }

            MapImage.Source = new Bitmap(filePath);
            zoomBorder.ZoomTo(0.8, 640, 640);
            
            fileLaps = await FindLaps(records, resultsURL);
            
            LoadingText.Text = "Plotting on the map...";
            
            lapIndex = 0;
            DisplayLap(fileLaps[lapIndex]);
            
            UpdateCompareDropdowns();
        }

        public void UpdateCompareDropdowns()
        {
            var flyout = new MenuFlyout();

            for (int i = 0; i < fileLaps.Count; i++)
            {
                Lap lap =  fileLaps[i];
                var item = new CheckBox() { Content = "Lap: " + (i+1).ToString() };
                item.IsCheckedChanged += OnAdditionalLaps;
                flyout.Items.Add(item);
            }

            LapsDropdown.Flyout = flyout;
        }

        private void OnAdditionalLaps(object? sender, RoutedEventArgs e)
        {
            if (sender.GetType() == typeof(CheckBox))
            {
                CheckBox checkBox = (CheckBox)sender;
                if (checkBox.IsChecked == true)
                {
                    DisplayLap(fileLaps[int.Parse(checkBox.Content.ToString().Split(':')[1].Trim())-1], true);
                }
                else
                {
                    currentlyDisplayed.Remove(fileLaps[int.Parse(checkBox.Content.ToString().Split(':')[1].Trim())-1]);
                    var temp = currentlyDisplayed;
                    currentlyDisplayed = new List<Lap>();
                    OverlayCanvas.Children.Clear();
                    foreach (var lap in temp)
                    {
                        DisplayLap(lap, true);
                    }
                }
            }
        }

        public DateTime ParseDateTime(string dateTimeString, string miliseconds)
        {
            if (DateTime.TryParse(dateTimeString, out var baseTime))
            {
                int ms = int.TryParse(miliseconds, out var tmp) ? tmp : 0;
                DateTime fullTime = baseTime.AddMilliseconds(ms);
                return fullTime;
            }

            return new DateTime();
        }
    }
}
