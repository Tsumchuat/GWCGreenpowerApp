using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;

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
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        throw new System.NotImplementedException();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(latBox.Text) || string.IsNullOrEmpty(lonBox.Text) || string.IsNullOrEmpty(zoomBox.Text))
        {
            await MessageBoxManager
                .GetMessageBoxStandardWindow("Error Saving location", "Values cannot be blank", ButtonEnum.Ok)
                .Show();
            return;
        }

        string name = await LocationSavePopup.ShowDialogAsync(_owner);
        if (string.IsNullOrEmpty(name))
        {
            await MessageBoxManager
                .GetMessageBoxStandardWindow("Error Saving location", "Name cannot be blank", ButtonEnum.Ok)
                .Show();
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
        this.Close();
    }
}