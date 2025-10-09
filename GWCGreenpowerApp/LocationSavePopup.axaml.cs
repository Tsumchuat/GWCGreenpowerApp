using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GWCGreenpowerApp;

public partial class LocationSavePopup : Window
{
    public string? PresetName { get; private set; }

    public LocationSavePopup()
    {
        InitializeComponent();
        
        OkButton.Click += (_, _) => OnOk();
        CancelButton.Click += (_, _) => Close(null);
    }
    
    private void OnOk()
    {
        PresetName = NameBox.Text?.Trim();
        Close(PresetName);
    }

    public static async Task<string?> ShowDialogAsync(Window owner)
    {
        var dialog = new LocationSavePopup();
        return await dialog.ShowDialog<string?>(owner);
    }

}