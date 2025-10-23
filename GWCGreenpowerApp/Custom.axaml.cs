using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace GWCGreenpowerApp;

public partial class Custom : Window
{
    private MainWindow _owner;
    public Custom(MainWindow owner)
    {
        InitializeComponent();

        analyse.Click += Analyse_Click;
        load.Click += Load_Click;
        save.Click += Save_Click;
        cancel.Click += Cancel_Click;

        _owner = owner;
        
        UpdateMenuLocations();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(latBox.Text) || string.IsNullOrEmpty(lonBox.Text) || string.IsNullOrEmpty(zoomBox.Text))
        {
            await MessageBoxManager
                .GetMessageBoxStandard("Error Saving location", "Values cannot be blank", ButtonEnum.Ok)
                .ShowAsync();
            return;
        }

        string name = await LocationSavePopup.ShowDialogAsync(_owner);
        if (string.IsNullOrEmpty(name))
        {
            await MessageBoxManager
                .GetMessageBoxStandard("Error Saving location", "Name cannot be blank", ButtonEnum.Ok)
                .ShowAsync();
            return;
        }
        
        Location location = new Location
        {
            name = name,
            lat = Convert.ToSingle(latBox.Text),
            lon = Convert.ToSingle(lonBox.Text),
            zoom = Convert.ToInt32(zoomBox.Text),
            lapInFile = (bool)lapsSwitch.IsChecked
        };
        
        LocationManager.SaveLocation(location);
    }

    private void Load_Click(object? sender, RoutedEventArgs e)
    {
        
    }

    private void Analyse_Click(object? sender, RoutedEventArgs e)
    {
        _owner.Analyse(Convert.ToSingle(latBox.Text), Convert.ToSingle(lonBox.Text), Convert.ToInt32(zoomBox.Text), (bool)lapsSwitch.IsChecked);
        _owner.location = new Location(){lat = Convert.ToSingle(latBox.Text), lon =Convert.ToSingle(lonBox.Text), zoom = Convert.ToInt32(zoomBox.Text), lapInFile = (bool)lapsSwitch.IsChecked};
        this.Close();
    }
    
    public async void UpdateMenuLocations()
    {
        List<Location> menuLocations = new List<Location>( await LocationManager.GetLocations());
        
        menuLocations.Add(new Location() { name = "-" });
        menuLocations.Add(new Location() { name = "Close" });
            
        var flyout = new MenuFlyout();

        foreach (var location in menuLocations)
        {
            var item = new MenuItem { Header = location.name };
            item.Click += OnLocationPicked;
            flyout.Items.Add(item);
        }
            
        load.Flyout = flyout;
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
            var locations = await LocationManager.GetLocations();
            var selectedLocation = locations.Find(loc => loc.name == locationName);
            if (selectedLocation != null)
            {
                latBox.Text = selectedLocation.lat.ToString();
                lonBox.Text = selectedLocation.lon.ToString();
                zoomBox.Text = selectedLocation.zoom.ToString();
                lapsSwitch.IsChecked = selectedLocation.lapInFile;
            }
        }
    }
}