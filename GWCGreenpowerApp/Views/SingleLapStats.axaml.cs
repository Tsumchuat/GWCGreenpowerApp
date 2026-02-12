using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GWCGreenpowerApp.Views;

public partial class SingleLapStats : UserControl
{
    public SingleLapStats()
    {
        InitializeComponent();
    }
    
    public void UpdateLap(Lap lap)
    {
        LapNum.Text = "Lap Number: " + lap.Number;
        LapStart.Text = "Start Time: " + lap.StartTime.ToString("HH:mm:ss");
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
    }
}