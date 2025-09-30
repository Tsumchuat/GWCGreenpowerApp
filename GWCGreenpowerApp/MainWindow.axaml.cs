using CsvHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace GWCGreenpowerApp
{
    public partial class MainWindow : Window
    {
        public string workingFile;
        static readonly HttpClient client = new HttpClient();
        float latitude = 0f;
        float longitude = 0f;
        int zoom = 17;

        public MainWindow()
        {
            InitializeComponent();
            
            //ui setup
            FileButton.Click += OnFileSelect;
            FilePath.TextChanged += OnFilePathChanged;
            AnalyseButton.Click += OnProcessFile;

            comboBox1.ItemsSource = new Locationss();
            comboBox1.SelectionChanged += OnLocationChanged;
        }

        async void OnFileSelect(object sender, RoutedEventArgs e)
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

        async void OnProcessFile(object sender, RoutedEventArgs e)
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

            if (File.Exists(filePath))
            {
                MapImage.Source = new Bitmap(filePath);
                // TODO: marker drawing – Avalonia doesn’t support DrawingVisual directly.
                // You’d draw overlays using a Canvas or SkiaSharp.
            }
            else
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Error", "Map image not found.", ButtonEnum.Ok)
                    .ShowAsync();
            }
        }

        private void OnFilePathChanged(object sender, TextChangedEventArgs e)
        {
            workingFile = FilePath.Text;
        }

        private void OnLocationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBox1.SelectedItem is string selectedLocation)
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
    }

    public class FileData
    {
        public string BluetoothConnected { get; set; }
        [CsvHelper.Configuration.Attributes.Name("DateTime (date)")]
        public string DateTime { get; set; }
        [CsvHelper.Configuration.Attributes.Name("GPS latitude (°)")]
        public float Latitude { get; set; }
        [CsvHelper.Configuration.Attributes.Name("GPS longitude (°)")]
        public float Longitude { get; set; }
    }

    public class Locationss : ObservableCollection<string>
    {
        public Locationss()
        {
            Add("Fife Cycle Track");
            Add("Bo'ness");
            Add("East Fortune");
        }
    }
}
