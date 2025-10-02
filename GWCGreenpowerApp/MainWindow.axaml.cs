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
        float latitude = 0f;
        float longitude = 0f;
        int zoom = 17;
        private int minLapDataCount = 30;
        private List<Lap> fileLaps = new List<Lap>();
        private int lapIndex = 0;
        float xoffset = 320;
        float yoffset = 320;
        public MainWindow()
        {
            InitializeComponent();
            
            //ui events setup
            FileButton.Click += OnFileSelect;
            FilePath.TextChanged += OnFilePathChanged;
            AnalyseButton.Click += OnProcessFile;
            comboBox1.SelectionChanged += OnLocationChanged;
            LeftButton.Click += OnLeftButton;
            RightButton.Click += OnRightButton;
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

        async void OnProcessFile(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(workingFile))
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
            string filePath = Path.Combine(Path.GetTempPath(), "GWCGreenpowermap.png");

            MapImage.Source = new Bitmap(filePath);
            if (!File.Exists(filePath))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error", "Map image not found.", ButtonEnum.Ok)
                    .ShowAsync();
                return;
                
                /*  OLD CODE (DISPLAYS ALL POINTS IN THE FILE IS ALSO WOULDNT WORK IN THIS IF STATEMENT ANYMORE)
                foreach (FileData record in records)
                {
                    var point = LatLonToPixel(
                        record.Latitude,
                        record.Longitude,
                        latitude,
                        longitude,
                        zoom,
                        (int)MapImage.Bounds.Width,
                        (int)MapImage.Bounds.Height);

                    var marker = new Ellipse
                    {
                        Width = 2,
                        Height = 2,
                        Fill = Brushes.Red
                    };
                    float xoffset = 320;
                    float yoffset = 320;
                    Canvas.SetLeft(marker, point.X - 1 + xoffset);
                    Canvas.SetTop(marker, point.Y - 1 + yoffset);
                    OverlayCanvas.Children.Add(marker);

                } */
            }
            
            fileLaps = FindLaps(records);
            lapIndex = 0;
            DisplayLap(fileLaps[lapIndex], lapIndex+1);
        }

        private void OnFilePathChanged(object? sender, TextChangedEventArgs e)
        {
            workingFile = FilePath.Text;
        }

        private void OnLocationChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (comboBox1.SelectedItem is ComboBoxItem item && item.Content is string selectedLocation)
            {
                switch (selectedLocation)
                {
                    case "Fife Cycle Track":
                        latitude = 56.140360f;
                        latbox.Text = "56.140360";
                        longitude = -3.320650f;
                        longibox.Text = "-3.320650";
                        zoom = 17;
                        zoombox.Text = "17";
                        break;
                    case "Bo'ness":
                        latitude = 56.008347f;
                        latbox.Text = "56.008347";
                        longitude = -3.635709f;
                        longibox.Text = "-3.635709";
                        zoom = 19;
                        zoombox.Text = "19";
                        break;
                    case "East Fortune":
                        latitude = 56.001370f;
                        latbox.Text = "56.001370";
                        longitude = -2.708461f;
                        longibox.Text = "-2.708461";
                        zoom = 16;
                        zoombox.Text = "16";
                        break;
                }
            }
        }

        static async Task GenerateMap(float lat, float longi, int zooms)
        {
            string url = $"https://maps.googleapis.com/maps/api/staticmap?center={lat},{longi}&zoom={zooms}&size=640x640&maptype=satellite&key=AIzaSyBTL-v9AZuWE66IECeZfcsbU07AAG-1FYc";
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync();
            string filePath = Path.Combine(Path.GetTempPath(), "GWCGreenpowermap.png");
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

            float px = point.X - center.X ;
            float py = point.Y - center.Y ;

            return new PointF(px, py);
        }
            
        public List<Lap> FindLaps(List<FileData> records)
        {
            List<Lap> laps = new List<Lap>();
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
                foreach (FileData record in lap.Data)
                {
                    if (lastLat != record.Latitude || lastLon != record.Longitude)
                    {
                        gpsMoved++;
                    }

                    lastLat = (float)record.Latitude;
                    lastLon = (float)record.Longitude;
                }

                if (gpsMoved < 20)
                {
                    lapsToRemove.Add(lap);
                    continue;
                }

                lap.MaxRPM = lap.Data.Max(d => d.RPM ?? 0);
                
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
        [CsvHelper.Configuration.Attributes.Name("Motor speed (RPM)")]
        public int? RPM { get; set; }
    }

    public class Lap
    {
        public List<FileData> Data { get; set; } = new List<FileData>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public float MaxSpeed { get; set; }
        public int MaxRPM { get; set; }
        public float MaxCurrent { get; set; }
        public float MinVoltage { get; set; }
    }

}
