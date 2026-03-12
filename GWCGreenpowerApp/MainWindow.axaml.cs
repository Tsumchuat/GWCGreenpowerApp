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
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Color = Avalonia.Media.Color;
using Path = System.IO.Path;

//TODO add a fancy loading screen but not high priority there is way more important stuff
namespace GWCGreenpowerApp
{
    public partial class MainWindow : Window
    {
        private static string mapsAPIKey = ""; //PUT YOUR MAPS API KEY HERE
        
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

        public ObservableCollection<Lap> currentlyDisplayed { get; set; }= new();

        // collections the chart reads from
        public ObservableCollection<ObservablePoint> VoltageValues = new();
        public ObservableCollection<ObservablePoint> CurrentValues = new();
        public ObservableCollection<ObservablePoint> SpeedValues = new();
        public ObservableCollection<ObservablePoint> RPMValues = new();

        // series objects
        private LineSeries<ObservablePoint> VoltageSeries;
        private LineSeries<ObservablePoint> CurrentSeries;
        private LineSeries<ObservablePoint> SpeedSeries;
        private LineSeries<ObservablePoint> RPMSeries;

        public ISeries[] Series { get; set; } //update livecharts2 to a version higher than 6.1 (the next release has a patch for a bug i had top rollback to 5.4 to solve) 
        
        public Axis[] YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Voltage (V)  Current (A)  Speed(mph)", 
                MinLimit = 0,
                MaxLimit = 32//todo make this configurage in the app
            },
            new Axis
            {
                Name = "RPM",  Position = AxisPosition.End,
                MinLimit = 0,
                MaxLimit = 2500
            },
        };
        
        public MainWindow()
        {
            InitializeComponent();

            if (Environment.GetEnvironmentVariable("MAPS_API_KEY") != null)
            {
                mapsAPIKey = Environment.GetEnvironmentVariable("MAPS_API_KEY");
            }
            
            //ui events setup
            FileButton.Click += OnFileSelect;
            FilePath.TextChanged += OnFilePathChanged;
            LeftButton.Click += OnLeftButton;
            RightButton.Click += OnRightButton;

            xOffsetBox.TextChanged += OnxOffset;
            yOffsetBox.TextChanged += OnyOffset;

            AnalyseTabs.SelectionChanged += OnTabChanged;
            
            UpdateMenuLocations();
            
            VoltageSeries = new LineSeries<ObservablePoint>
            {
                Values = VoltageValues,
                Name = "Voltage",
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                ScalesYAt = 0
            };

            CurrentSeries = new LineSeries<ObservablePoint>
            {
                Values = CurrentValues,
                Name = "Current",
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                ScalesYAt = 0
            };

            SpeedSeries = new LineSeries<ObservablePoint>
            {
                Values = SpeedValues,
                Name = "Speed",
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                ScalesYAt = 0
            };

            RPMSeries = new LineSeries<ObservablePoint>
            {
                Values = RPMValues,
                Name = "RPM",
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                ScalesYAt = 1
            };

            Series = new ISeries[]
            {
                VoltageSeries,
                CurrentSeries,
                SpeedSeries,
                RPMSeries,
            };
            
            DataContext = this;
        }
        
        //for customising the per lap graph 
        private void Voltage_On(object? s, RoutedEventArgs e) => VoltageSeries.IsVisible = true;
        private void Voltage_Off(object? s, RoutedEventArgs e) => VoltageSeries.IsVisible = false;

        private void Current_On(object? s, RoutedEventArgs e) => CurrentSeries.IsVisible = true;
        private void Current_Off(object? s, RoutedEventArgs e) => CurrentSeries.IsVisible = false;

        private void Speed_On(object? s, RoutedEventArgs e) => SpeedSeries.IsVisible = true;
        private void Speed_Off(object? s, RoutedEventArgs e) => SpeedSeries.IsVisible = false;

        private void RPM_On(object? s, RoutedEventArgs e) => RPMSeries.IsVisible = true;
        private void RPM_Off(object? s, RoutedEventArgs e) => RPMSeries.IsVisible = false;

        private void OnTabChanged(object? sender, SelectionChangedEventArgs e) //TODO fix changing the offsets just displays the other tabs lap IMPORTANT proably just make sure when changed it redraws currently selected not lapindex
        {
            if (sender is TabControl tab && tab.SelectedItem is TabItem selectedTab)
            {
                switch (selectedTab.Header.ToString())
                {
                    case "Compare":
                        LeftButton.IsEnabled = false;
                        RightButton.IsEnabled = false;
                        LeftButton.IsVisible = false;
                        RightButton.IsVisible = false;
                        OverlayCanvas.Children.Clear();
                        currentlyDisplayed.Clear();;
                        zoomBorder.IsVisible = true;
                        zoomBorder.IsEnabled = true;
                        MainGraph.IsEnabled = false;
                        MainGraph.Opacity = 0;
                        break;
                    case "Stats":
                        LeftButton.IsEnabled = true; //prety sure i dont need to specify if i hide it but idk
                        RightButton.IsEnabled = true;
                        LeftButton.IsVisible = true;
                        RightButton.IsVisible = true;
                        lapIndex = 0;
                        zoomBorder.IsVisible = true;
                        zoomBorder.IsEnabled = true;
                        MainGraph.IsEnabled = false;
                        MainGraph.Opacity = 0;
                        if (fileLaps.Count < 1) break;
                        DisplayLap(fileLaps[lapIndex]);
                        break;
                    case "Graph":
                        LeftButton.IsEnabled = true;
                        RightButton.IsEnabled = true;
                        LeftButton.IsVisible = true;
                        RightButton.IsVisible = true;
                        OverlayCanvas.Children.Clear();
                        currentlyDisplayed.Clear();;
                        zoomBorder.IsVisible = false;
                        zoomBorder.IsEnabled = false;
                        MainGraph.YAxes = YAxes;
                        MainGraph.IsEnabled = true;
                        MainGraph.Opacity = 1;
                        if (fileLaps.Count < 1) break;
                        DisplayLap(fileLaps[lapIndex]);
                        break;
                }
            }
        }

        private void OnyOffset(object? sender, TextChangedEventArgs e)
        {
            float.TryParse(yOffsetBox.Text, out useryOffset);
            if (fileLaps.Count > 0)
            {
                var temp = new ObservableCollection<Lap>(currentlyDisplayed);
                currentlyDisplayed.Clear();;
                OverlayCanvas.Children.Clear();
                foreach (Lap lap in temp)
                {
                    DisplayLap(lap, true);
                }
            }
        }

        private void OnxOffset(object? sender, TextChangedEventArgs e)
        {
            float.TryParse(xOffsetBox.Text, out userXOffset);
            if (fileLaps.Count > 0)
            {
                var temp = new ObservableCollection<Lap>(currentlyDisplayed);
                currentlyDisplayed.Clear();;
                OverlayCanvas.Children.Clear();
                foreach (Lap lap in temp)
                {
                    DisplayLap(lap, true);
                }
            }
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
            string url = $"https://maps.googleapis.com/maps/api/staticmap?center={lat},{longi}&zoom={zooms}&size=640x640&maptype=satellite&scale=2&key={mapsAPIKey}";
            using HttpResponseMessage response = await client.GetAsync(url);//TODO fix this if wifi is off
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

                    if (i == lap.Data.Count - 1 && !finishedLap)
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
                lap.EstimatedTime = (endTime - startTime) - new TimeSpan(0, 0, 0, 0, 900); //subtract time to account for human delay after crossing the line
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
                laps[i].Colour = GetBurshColourForLap(laps[i].Number);
            }

            return laps;
        }

        void DisplayLap(Lap lap, bool additional = false)
        {
            if (!additional)
            {
                OverlayCanvas.Children.Clear();
                currentlyDisplayed.Clear();;
            } 
            currentlyDisplayed.Add(lap);
            
            PointF? oldPoint = null;
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

                marker.Fill = GetBurshColourForLap(lap.Number);

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
                    EndPoint = new Avalonia.Point(point.X, point.Y),
                    Opacity = 0.5f
                };

                line.Stroke = GetBurshColourForLap(lap.Number);
        
                Canvas.SetLeft(line, xoffset + userXOffset);
                Canvas.SetTop(line, yoffset + useryOffset);
                OverlayCanvas.Children.Add(line);
                
                oldPoint = point;
            }

            StatsView.UpdateLap(lap);
            GraphView.UpdateLap(lap);
            
            AnalyseLoading.IsVisible = false;
            
            if (MainGraph.IsEnabled)
            {
                VoltageValues.Clear();
                CurrentValues.Clear();
                SpeedValues.Clear();
                RPMValues.Clear();
                //fixes the smaller data leaving ghost points bug
                VoltageSeries.Invalidate(MainGraph.CoreChart);
                CurrentSeries.Invalidate(MainGraph.CoreChart);
                SpeedSeries.Invalidate(MainGraph.CoreChart);
                RPMSeries.Invalidate(MainGraph.CoreChart); 
                
                foreach (FileData fileData in lap.Data)
                {
                    double seconds = (ParseDateTime(fileData.DateTimeString, fileData.Miliseconds) - lap.StartTime).TotalSeconds;
                    if (seconds < 0)
                    {
                        continue;
                    }
                    VoltageValues.Add(new ObservablePoint(seconds, fileData.Voltage));
                    CurrentValues.Add(new ObservablePoint(seconds, fileData.Current));
                    SpeedValues.Add(new ObservablePoint(seconds, fileData.Speed));
                    RPMValues.Add(new ObservablePoint(seconds, fileData.RPM)); 
                }
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var s in Series)
                        s.Invalidate(MainGraph.CoreChart);
                });
            }
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
            if (fileLaps.Count < 1)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error", $"Failed to find laps in file", ButtonEnum.Ok)
                    .ShowAsync();
            }
            else DisplayLap(fileLaps[lapIndex]);
            
            UpdateCompareDropdowns(fileLaps);
            UpdateFilterDropdowns();

            AnalyseTabs.SelectedIndex = 0;
        }

        public void UpdateCompareDropdowns(List<Lap> laps)
        {
            var flyout = new MenuFlyout();

            for (int i = 0; i < laps.Count; i++)
            {
                Lap lap =  laps[i];
                var item = new CheckBox() { Content = "Lap: " + lap.Number };
                item.IsCheckedChanged += OnAdditionalLaps;
                flyout.Items.Add(item);
            }

            LapsDropdown.Flyout = flyout;
        }

        public void UpdateFilterDropdowns()
        {
            List<string> names = new List<string>();
            List<string> cars = new List<string>();
            foreach (Lap lap in fileLaps)
            {
                if (!names.Contains(lap.Driver))
                {
                    names.Add(lap.Driver);
                }

                if (!cars.Contains(lap.Car))
                {
                    cars.Add(lap.Car);
                }
            }

            var flyout = new MenuFlyout();
            foreach (string name in names)
            {
                var item = new MenuItem() { Header = name };
                item.Click += OnNameFilter;
                flyout.Items.Add(item);
            }
            flyout.Items.Add(new Separator());
            var allname = new MenuItem() { Header = "All" };
            allname.Click += OnNameFilter;
            flyout.Items.Add(allname);
            NameFilter.Flyout =  flyout;
            
            flyout = new MenuFlyout();
            foreach (string car in cars)
            {
                var item = new MenuItem() { Header = car };
                item.Click += OnCarFilter;
                flyout.Items.Add(item);
            }
            flyout.Items.Add(new Separator());
            var allcar = new MenuItem() { Header = "All" };
            allcar.Click += OnCarFilter;
            flyout.Items.Add(allcar);
            CarFilter.Flyout =  flyout;
        }

        private void OnCarFilter(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Header is string car)
            {
                if (car == "All")
                {
                    UpdateCompareDropdowns(fileLaps);
                    CarFilter.Content = "Cars";
                    NameFilter.Content = "Names";
                }
                else
                {
                    FilterCompareDropdownByCar(car);
                    CarFilter.Content = car;
                    NameFilter.Content = "Names";
                }
            }
        }

        private void OnNameFilter(object? sender, RoutedEventArgs e)
        {
            
            if (sender is MenuItem menuItem && menuItem.Header is string name)
            {
                if (name == "All")
                {
                    UpdateCompareDropdowns(fileLaps);
                    NameFilter.Content = "Names";
                    CarFilter.Content = "Cars";
                }
                else
                {
                    FilterCompareDropdownByName(name);
                    NameFilter.Content = name;
                    CarFilter.Content = "Cars";
                }
            }
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
                    ObservableCollection<Lap> temp =  new ObservableCollection<Lap>(currentlyDisplayed);
                    currentlyDisplayed.Clear();
                    OverlayCanvas.Children.Clear();
                    foreach (Lap lap in temp)
                    {
                        DisplayLap(lap, true);
                    }
                }
            }
            var sorted = currentlyDisplayed.OrderBy(lap => lap.Number).ToList();
            currentlyDisplayed.Clear();
            foreach (var lap in sorted)
            {
                currentlyDisplayed.Add(lap);
            }
        }

        public void FilterCompareDropdownByName(string name)
        {
            List<Lap> laps = new List<Lap>();
            foreach (Lap lap in fileLaps)
            {
                if (lap.Driver == name)
                {
                    laps.Add(lap);
                }
            }
            UpdateCompareDropdowns(laps);
            OverlayCanvas.Children.Clear();
        }
        
        public void FilterCompareDropdownByCar(string car)
        {
            List<Lap> laps = new List<Lap>();
            foreach (Lap lap in fileLaps)
            {
                if (lap.Car == car)
                {
                    laps.Add(lap);
                }
            }
            UpdateCompareDropdowns(laps);
            OverlayCanvas.Children.Clear();
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

        public IBrush GetBurshColourForLap(int number)
        {
            var rand = new Random(number * 10000); // Seed based on lap number
            byte r = (byte)rand.Next(100, 256); // Avoid super dark colors
            byte g = (byte)rand.Next(100, 256);
            byte b = (byte)rand.Next(100, 256);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
