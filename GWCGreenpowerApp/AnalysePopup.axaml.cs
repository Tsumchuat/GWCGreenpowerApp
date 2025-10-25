using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace GWCGreenpowerApp;

public partial class AnalysePopup : Window
{
    public string url = "";
    public AnalysePopup()
    {
        InitializeComponent();
    }


    private void OnAnalyse(object? sender, RoutedEventArgs e)
    {
        url = ResultsURL.Text;
        this.Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}