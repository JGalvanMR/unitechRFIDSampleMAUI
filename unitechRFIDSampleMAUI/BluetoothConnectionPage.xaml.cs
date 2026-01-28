using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Android.Util;

namespace unitechRFIDSampleMAUI;

public partial class BluetoothConnectionPage : ContentPage
{
    private Com.Unitech.Lib.Types.DeviceType deviceType;
    private readonly Regex macAddressRegex = new Regex(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$");
    private List<string> recentMacAddresses;

    public BluetoothConnectionPage(Com.Unitech.Lib.Types.DeviceType selectedDevice)
    {
        InitializeComponent();
        deviceType = selectedDevice;

        // Set device type label
        DeviceTypeLabel.Text = deviceType.ToString();

        // Load recent MAC addresses
        LoadRecentMacAddresses();

        // Set initial state
        UpdateConnectButtonState();
    }

    private void LoadRecentMacAddresses()
    {
        // Load from preferences (you can implement persistent storage here)
        recentMacAddresses = new List<string>();

        // Example of loading from preferences
        var savedAddresses = Preferences.Get($"RecentMac_{deviceType}", "");
        if (!string.IsNullOrEmpty(savedAddresses))
        {
            recentMacAddresses = savedAddresses.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        // Show recent addresses if available
        if (recentMacAddresses.Any())
        {
            RecentAddressesFrame.IsVisible = true;
            PopulateRecentAddresses();
        }
    }

    private void PopulateRecentAddresses()
    {
        RecentAddressesList.Children.Clear();

        foreach (var address in recentMacAddresses.Take(5)) // Show only last 5
        {
            var button = new Button
            {
                Text = address,
                BackgroundColor = Color.FromArgb("#F0F0F0"),
                TextColor = Color.FromArgb("#333333"),
                FontSize = 12,
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 2),
                CornerRadius = 5,
                HorizontalOptions = LayoutOptions.Fill
            };

            button.Clicked += (sender, e) =>
            {
                MacAddressEntry.Text = address;
                ValidateMacAddress(address);
            };

            RecentAddressesList.Children.Add(button);
        }
    }

    private bool _isUpdatingMacEntry = false;

    private void OnMacAddressTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingMacEntry) return;

        var entry = sender as Entry;
        if (entry == null) return;
        var text = e.NewTextValue ?? string.Empty;

        // Format MAC address
        var formatted = FormatMacAddress(text);

        // Only update if formatting changed
        if (formatted != entry.Text)
        {
            try
            {
                _isUpdatingMacEntry = true;

                UpdateEntryText(formatted);
            }
            finally
            {
                _isUpdatingMacEntry = false;
            }
            return;
        }

        // Validate and update UI
        ValidateMacAddress(formatted);
        UpdateConnectButtonState();
    }

    private void UpdateEntryText(string text)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                MacAddressEntry.Text = text;

                // Set cursor position
                var cursor = Math.Min(text.Length, MacAddressEntry.Text?.Length ?? 0);
                MacAddressEntry.CursorPosition = cursor;
            }
            finally
            {
                _isUpdatingMacEntry = false;
            }
        });
    }

    private string FormatMacAddress(string input)
    {
        // Remove any existing separators and non-hex characters
        var cleanInput = Regex.Replace(input.ToUpper(), @"[^0-9A-F]", "");

        // Limit to 12 characters (6 pairs)
        if (cleanInput.Length > 12)
            cleanInput = cleanInput.Substring(0, 12);

        // Add colons every 2 characters
        var formatted = "";
        for (int i = 0; i < cleanInput.Length; i += 2)
        {
            if (i > 0) formatted += ":";
            formatted += cleanInput.Substring(i, Math.Min(2, cleanInput.Length - i));
        }

        return formatted;
    }

    private void ValidateMacAddress(string macAddress)
    {
        if (string.IsNullOrEmpty(macAddress))
        {
            ValidationLabel.IsVisible = false;
            return;
        }

        if (macAddressRegex.IsMatch(macAddress))
        {
            ValidationLabel.IsVisible = false;
        }
        else
        {
            ValidationLabel.Text = "Invalid MAC address format. Use XX:XX:XX:XX:XX:XX";
            ValidationLabel.IsVisible = true;
        }
    }

    private void UpdateConnectButtonState()
    {
        var macAddress = MacAddressEntry.Text ?? string.Empty;
        ConnectBtn.IsEnabled = macAddressRegex.IsMatch(macAddress);
    }

    private async void OnConnectClicked(object sender, EventArgs e)
    {
        var macAddress = MacAddressEntry.Text?.Trim();

        if (string.IsNullOrEmpty(macAddress) || !macAddressRegex.IsMatch(macAddress))
        {
            await DisplayAlert("Invalid MAC Address", "Please enter a valid MAC address in the format XX:XX:XX:XX:XX:XX", "OK");
            return;
        }

        // Show connection status
        ShowConnectionStatus("Connecting...", true);
        ConnectBtn.IsEnabled = false;

        try
        {
            // Save to recent addresses
            SaveRecentMacAddress(macAddress);

            // Navigate to device page with Bluetooth connection
            await Navigation.PushAsync(new DevicePage(deviceType, macAddress));

            // Remove this page from the navigation stack
            Navigation.RemovePage(this);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Connection Error", $"Failed to connect: {ex}", "OK");
            HideConnectionStatus();
            UpdateConnectButtonState();
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void ShowConnectionStatus(string message, bool showIndicator)
    {
        StatusLabel.Text = message;
        ConnectionIndicator.IsRunning = showIndicator;
        StatusFrame.IsVisible = true;
    }

    private void HideConnectionStatus()
    {
        StatusFrame.IsVisible = false;
        ConnectionIndicator.IsRunning = false;
    }

    private void SaveRecentMacAddress(string macAddress)
    {
        // Remove if already exists to avoid duplicates
        recentMacAddresses.RemoveAll(addr => addr.Equals(macAddress, StringComparison.OrdinalIgnoreCase));

        // Add to the beginning of the list
        recentMacAddresses.Insert(0, macAddress);

        // Keep only the most recent 10 addresses
        if (recentMacAddresses.Count > 10)
        {
            recentMacAddresses = recentMacAddresses.Take(10).ToList();
        }

        // Save to preferences
        var addressesString = string.Join(";", recentMacAddresses);
        Preferences.Set($"RecentMac_{deviceType}", addressesString);

        // Update UI
        if (!RecentAddressesFrame.IsVisible)
        {
            RecentAddressesFrame.IsVisible = true;
        }
        PopulateRecentAddresses();
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle hardware back button on Android
        Dispatcher.Dispatch(async () =>
        {
            await Navigation.PopAsync();
        });
        return true;
    }
}
