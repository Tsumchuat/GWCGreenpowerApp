using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        throw new System.NotImplementedException();
    }

    private void Load_Click(object? sender, RoutedEventArgs e)
    {
        throw new System.NotImplementedException();
    }

    private void Analyse_Click(object? sender, RoutedEventArgs e)
    {
        _owner.Analyse(Convert.ToSingle(latBox.Text), Convert.ToSingle(lonBox.Text), Convert.ToInt32(zoomBox.Text), (bool)lapsSwitch.IsChecked);
        this.Close();
    }
}