using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using Android.Widget;
using AndroidX.Emoji2.Text.FlatBuffer;
using Com.Unitech.Api.Keymap;
using Com.Unitech.Lib.Diagnostics;
using Com.Unitech.Lib.Eax;
using Com.Unitech.Lib.Engine.Types;
using Com.Unitech.Lib.Event.Params;
using Com.Unitech.Lib.Htx;
using Com.Unitech.Lib.Pax;
using Com.Unitech.Lib.Reader;
using Com.Unitech.Lib.Reader.Event;
using Com.Unitech.Lib.Reader.Params;
using Com.Unitech.Lib.Reader.Types;
using Com.Unitech.Lib.Rgx;
using Com.Unitech.Lib.Rpx;
using Com.Unitech.Lib.Tagcoder;
using Com.Unitech.Lib.Transport;
using Com.Unitech.Lib.Transport.Types;
using Com.Unitech.Lib.Types;
using Com.Unitech.Lib.Uhf.Params;
using Com.Unitech.Lib.Uhf.Types;
using Com.Unitech.Lib.Util;
using Microsoft.Maui;
using System.Formats.Asn1;
using unitechRFIDSampleMAUI.Enums;
using unitechRFIDSampleMAUI.Resources;

namespace unitechRFIDSampleMAUI;

public partial class DevicePage : ContentPage
{
    private readonly int NIBLE_SIZE = 4;
    private readonly int histogramMax = -35;
    private readonly int histogramMin = -80;
    private readonly string rfidGunPressed = "com.unitech.RFID_GUN.PRESSED";
    private readonly string rfidGunReleased = "com.unitech.RFID_GUN.RELEASED";
    private readonly string systemExtendedPort = "com.unitech.EXTENDED_PORT";
    private readonly string keymappingPath = "/storage/emulated/0/Android/data/com.unitech.unitechrfidsamplemaui";
    private readonly string android12keymappingPath = "/storage/emulated/0/Unitech/unitechrfidsamplemaui/";

    private readonly string systemUssTriggerScan = "unitech.scanservice.software_scankey";
    private readonly string ExtraScan = "scan";

    // Android receiver fields
    private BroadcastReceiver mReceiver;
    private bool isReceiverRegistered = false;

    private Com.Unitech.Lib.Types.DeviceType deviceType;
    private BaseReader baseReader;
    private ReaderEventHandler readerEventHandler;
    private RfidEventHandler rfidEventHandler;
    private BarcodeEventHandler barcodeEventHandler;
    private UsbEventHandler usbEventHandler;
    private bool isusb = false;
    private string bluetoothMacAddress = string.Empty;
    private bool _isFindTag = false;
    private ConnectState _connectState = ConnectState.Disconnected;
    private DetectReader detectReader;
    private bool accessTagResult = false;
    private Bundle tempKeyCode = null;


    public DevicePage(Com.Unitech.Lib.Types.DeviceType selectedDevice)
    : this(selectedDevice, false)
    {
    }

    public DevicePage(Com.Unitech.Lib.Types.DeviceType selectedDevice, bool isUsb)
        : this(selectedDevice, isUsb, string.Empty)
    {
    }

    public DevicePage(Com.Unitech.Lib.Types.DeviceType selectedDevice, string bluetoothMac)
        : this(selectedDevice, false, bluetoothMac)
    {
    }

    public DevicePage(Com.Unitech.Lib.Types.DeviceType selectedDevice, bool isUsb, string bluetoothMac)
    {
        InitializeComponent();

        // Initialize Android-specific listeners
        Log.Info("DevicePage", $"Initializing DevicePage for device type: {selectedDevice}");
        readerEventHandler = new ReaderEventHandler(this);
        rfidEventHandler = new RfidEventHandler(this);
        barcodeEventHandler = new BarcodeEventHandler(this);
        usbEventHandler = new UsbEventHandler(this);
        Log.Debug("DevicePage", "Event listeners initialized successfully");

        deviceType = selectedDevice;
        isusb = isUsb;
        bluetoothMacAddress = bluetoothMac ?? string.Empty;
        ResetLabels();
        ResetProgress();

        InitUI();

        Log.Info("DevicePage", $"DevicePage constructor completed - Device: {deviceType}, USB: {isusb}, Bluetooth: {!string.IsNullOrEmpty(bluetoothMacAddress)}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            Log.Info("DevicePage", "OnAppearing - Initializing device page");

            EnableFunctions(false);

            // Additional setup if needed
            _ = Task.Run(ConnectTask);

            EnableReceiver(true);
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnAppearing: {ex}");
            await DisplayAlert("Error", "Failed to initialize the application properly.", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Clean up resources if needed
        if (baseReader != null)
        {
            try
            {
                baseReader.RemoveListener(readerEventHandler);
                baseReader.SetRfidEventListener(null);
                baseReader.Disconnect();
                baseReader.Dispose();
                baseReader = null;
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", "Error during cleanup: " + ex.ToString());
            }
        }

        RestoreGunKeyCode();

        EnableReceiver(false);
    }

    private async void OnInfoClicked(object sender, EventArgs e)
    {
        string deviceInfo = GetDeviceInfo();
        await DisplayAlert("Device Information", deviceInfo, "OK");
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        string settingsInfo = GetDeviceSettings();
        await DisplayAlert("Device Settings", settingsInfo, "OK");
    }

    #region Menu Event Handlers

    private async void OnFindDeviceClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            FindDevice findDevice = new FindDevice(FindDeviceMode.VibrateBeep, 10);

            baseReader.SetFindDevice(findDevice);
        }
        catch (Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnFindDeviceClicked: {ex}");
            await DisplayAlert("Error", $"Failed to find devices: {ex}", "OK");
        }
    }

    private async void OnChangeReadModeClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            ReadMode readMode = baseReader.ReadMode;

            if (readMode == ReadMode.MultiRead)
            {
                baseReader.ReadMode = ReadMode.SingleRead;
            }
            else if (readMode == ReadMode.SingleRead)
            {
                baseReader.ReadMode = ReadMode.MultiRead;
            }
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnChangeReadModeClicked: {ex}");
            await DisplayAlert("Error", $"Reader not available: {ex}", "OK");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnChangeReadModeClicked: {ex}");
            await DisplayAlert("Error", $"Failed to change read mode: {ex}", "OK");
        }
    }

    private async void OnChangeConnectionModeClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            baseReader.ConnectionMode = ConnectionMode.BtHID;
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnChangeConnectionModeClicked: {ex}");
            await DisplayAlert("Error", $"Reader not available: {ex}", "OK");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnChangeConnectionModeClicked: {ex}");
            await DisplayAlert("Error", $"Failed to change connection mode: {ex}", "OK");
        }
    }

    private async void OnFactoryResetClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            baseReader.FactoryReset();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnFactoryResetClicked: {ex}");
            await DisplayAlert("Error", $"Reader not available: {ex}", "OK");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnFactoryResetClicked: {ex}");
            await DisplayAlert("Error", $"Failed to perform factory reset: {ex}", "OK");
        }
    }

    private async void OnSetRegionUSClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            // Use the correct property to set region
            baseReader.RfidUhf.GlobalBand = GlobalBandType.Usa;
            await DisplayAlert("Region Set", "Region set to US", "OK");
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnSetRegionUSClicked: {ex}");
            await DisplayAlert("Error", $"Failed to set region: {ex}", "OK");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnSetRegionUSClicked: {ex}");
            await DisplayAlert("Error", $"Failed to set region: {ex}", "OK");
        }
    }

    private async void OnSetRegionEUClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            // Use the correct property to set region
            baseReader.RfidUhf.GlobalBand = GlobalBandType.Europe;
            await DisplayAlert("Region Set", "Region set to Europe", "OK");
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnSetRegionEUClicked: {ex}");
            await DisplayAlert("Error", $"Failed to set region: {ex}", "OK");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnSetRegionEUClicked: {ex}");
            await DisplayAlert("Error", $"Failed to set region: {ex}", "OK");
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();

            string textToDisplay = DisplayTextEntry.Text?.Trim();

            if (string.IsNullOrEmpty(textToDisplay))
            {
                textToDisplay = "";
            }

            if (baseReader.Action == ActionState.Stop)
            {
                SetDisplayOutput(2, true, textToDisplay);
            }
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnSendClicked: {ex}");
            await DisplayAlert("Error", $"Reader not available: {ex}", "OK");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnSendClicked: {ex}");
            await DisplayAlert("Error", $"Failed to send text to device: {ex}", "OK");
        }
    }

    #endregion

    private void OnInventoryClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnInventoryClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, ex.ToString(), ToastLength.Short).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            DoInventory();

        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private void OnCustomInventoryClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnCustomInventoryClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, ex.ToString(), ToastLength.Short).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            DoCustomInventory();

        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private void OnFindTagClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnFindTagClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, ex.ToString(), ToastLength.Short).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            DoFind();
        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private void OnReadClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnReadClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, ex.ToString(), ToastLength.Short).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            ClearResult();
            DoRead();
        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private void OnWriteClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnWriteClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, ex.ToString(), ToastLength.Short).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            ClearResult();
            DoWrite();
        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private async void OnLockClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnLockClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, "Lock error: " + ex.ToString(), ToastLength.Long).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            ClearResult();
            await Task.Run(() => LockUnlockProc(true));
        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private async void OnUnlockClicked(object sender, EventArgs e)
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in OnUnlockClicked: " + ex.ToString());
            Toast.MakeText(Android.App.Application.Context, "Unlock error: " + ex.ToString(), ToastLength.Long).Show();
            return;
        }

        if (baseReader.Action == ActionState.Stop)
        {
            ClearResult();
            await Task.Run(() => LockUnlockProc(false));
        }
        else if (baseReader.Action == ActionState.Inventory6c)
        {
            DoStop();
        }
    }

    private void OnFastIdToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            bool isFastIdEnabled = e.Value;

            try
            {
                // Set Fast ID mode on the RFID configuration
                baseReader.RfidUhf.FastID = isFastIdEnabled;

                Log.Info("DevicePage", $"Fast ID mode set to: {isFastIdEnabled}");

                // Optionally show a toast or update UI to indicate the change
                string status = isFastIdEnabled ? "enabled" : "disabled";
                Log.Debug("DevicePage", $"Fast ID {status}");
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error setting Fast ID mode: {ex}");

                // Reset the switch to its previous state if setting failed
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
                {
                    FastIdSwitch.IsToggled = !isFastIdEnabled;
                });
            }
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnFastIdToggled: {ex}");

            // Reset the switch on error
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                FastIdSwitch.IsToggled = false;
            });
        }
    }

    private void OnGen2xToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            bool isGen2xEnabled = e.Value;

            try
            {
                baseReader.RfidUhf.RFMode = isGen2xEnabled ? RFMode.Rf4123 : RFMode.Rf244;
                string mode = isGen2xEnabled ? RFMode.Rf4123.ToString() : RFMode.Rf244.ToString();
                Log.Info("DevicePage", $"RF Mode set to : {mode}");

                // Optionally show a toast or update UI to indicate the change
                string status = isGen2xEnabled ? "enabled" : "disabled";
                Log.Debug("DevicePage", $"Gen2x {status}");
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error setting RF Mode: {ex}");

                // Reset the switch to its previous state if setting failed
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
                {
                    Gen2xSwitch.IsToggled = !isGen2xEnabled;
                });
            }
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error in OnGen2xToggled: {ex}");

            // Reset the switch on error
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                Gen2xSwitch.IsToggled = false;
            });
        }
    }

    private string GetDeviceInfo()
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in GetDeviceInfo: " + ex.ToString());
            return "Error retrieving device information.";
        }

        var data = new System.Text.StringBuilder();

        try
        {
            data.AppendLine(AppStrings.DeviceName + ": " + baseReader.DeviceName);
            data.AppendLine(AppStrings.SKU + ": " + baseReader.SKU);
            data.AppendLine(AppStrings.Region + ": " + baseReader.RfidUhf.GlobalBand);
            data.AppendLine(AppStrings.Version + ": " + baseReader.Version);
            data.AppendLine(AppStrings.RfidModule + ": " + baseReader.RfidUhf.Type);

            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Pa768e
                || deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902
                || deviceType == Com.Unitech.Lib.Types.DeviceType.Rp300
                || deviceType == Com.Unitech.Lib.Types.DeviceType.Ea530)
            {
                data.AppendLine(AppStrings.SerialNumber + ": " + baseReader.SerialNo);
            }

            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp300)
            {
                data.AppendLine(AppStrings.RfidModuleVersion + ": " + baseReader.RfidUhf.Version);
                data.AppendLine(AppStrings.EngineModule + ": " + baseReader.EngineModel);
                data.AppendLine(AppStrings.EngineModuleVersion + ": " + baseReader.EngineVersion);
            }

            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902
                || deviceType == Com.Unitech.Lib.Types.DeviceType.Rp300)
            {
                // Convert Java Date to .NET DateTime format
                var javaTime = baseReader.Time;
                string timeString = javaTime != null ?
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMilliseconds(javaTime.Time).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") :
                    "N/A";
                data.AppendLine(AppStrings.Time + ": " + timeString);
                data.AppendLine(AppStrings.Beep + ": " + baseReader.Beeper.ToString());
            }

            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
            {
                data.AppendLine(AppStrings.ReadMode + ": " + baseReader.ReadMode.ToString());
                data.AppendLine(AppStrings.Vibrator + ": " + baseReader.Vibrator.ToString());
            }
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "Error retrieving device information: " + e.ToString());
        }

        return data.ToString();
    }

    private string GetDeviceSettings()
    {
        try
        {
            AssertReader();
        }
        catch (Java.Lang.Exception ex)
        {
            Log.Error("DevicePage", "Error in GetDeviceSettings: " + ex.ToString());
            return "Error retrieving device settings.";
        }

        var settings = new System.Text.StringBuilder();

        try
        {
            RFIDConfig rfidConfig = baseReader.RfidUhf.Config;

            var supportedParams = rfidConfig.SupportedParams;

            foreach (var paramId in supportedParams)
            {
                if (paramId == UhfParamId.Power)
                {
                    PowerRange powerRange = baseReader.RfidUhf.PowerRange;
                    settings.AppendLine($"{AppStrings.Power}: {rfidConfig.Power} ({powerRange.Min} - {powerRange.Max})");
                }
                else if (paramId == UhfParamId.ContinuousMode)
                {
                    settings.AppendLine($"{AppStrings.ContinuousMode}: {rfidConfig.ContinuousMode}");
                }
                else if (paramId == UhfParamId.InventoryTime)
                {
                    settings.AppendLine($"{AppStrings.InventoryTime}: {rfidConfig.InventoryTime}");
                }
                else if (paramId == UhfParamId.IdleTime)
                {
                    settings.AppendLine($"{AppStrings.IdleTime}: {rfidConfig.IdleTime}");
                }
                else if (paramId == UhfParamId.AlgorithmType)
                {
                    settings.AppendLine($"{AppStrings.Algorithm}: {rfidConfig.Algorithm}");
                }
                else if (paramId == UhfParamId.StartQ)
                {
                    settings.AppendLine($"{AppStrings.StartQ}: {rfidConfig.StartQ}");
                }
                else if (paramId == UhfParamId.MaxQ)
                {
                    settings.AppendLine($"{AppStrings.MaxQ}: {rfidConfig.MaxQ}");
                }
                else if (paramId == UhfParamId.MinQ)
                {
                    settings.AppendLine($"{AppStrings.MinQ}: {rfidConfig.MinQ}");
                }
                else if (paramId == UhfParamId.Target)
                {
                    settings.AppendLine($"{AppStrings.Target}: {rfidConfig.Target}");
                }
                else if (paramId == UhfParamId.Session)
                {
                    settings.AppendLine($"{AppStrings.Session}: {rfidConfig.Session}");
                }
                else if (paramId == UhfParamId.SelectMode)
                {
                    settings.AppendLine($"{AppStrings.SelectMode}: {rfidConfig.SelectMode}");
                }
                else if (paramId == UhfParamId.Encoding)
                {
                    settings.AppendLine($"{AppStrings.Encoding}: {rfidConfig.Encoding}");
                }
                else if (paramId == UhfParamId.Tari)
                {
                    settings.AppendLine($"{AppStrings.Tari}: {rfidConfig.TariType}");
                }
                else if (paramId == UhfParamId.Blf)
                {
                    settings.AppendLine($"{AppStrings.BLF}: {rfidConfig.BlfType}");
                }
                else if (paramId == UhfParamId.ToggleTarget)
                {
                    settings.AppendLine($"{AppStrings.ToggleTarget}: {rfidConfig.ToggleTarget}");
                }
                else if (paramId == UhfParamId.FastMode)
                {
                    settings.AppendLine($"{AppStrings.FastMode}: {rfidConfig.FastMode}");
                }
                else if (paramId == UhfParamId.DwellTime)
                {
                    settings.AppendLine($"{AppStrings.DwellTime}: {rfidConfig.DwellTime}");
                }
                else if (paramId == UhfParamId.RfMode)
                {
                    settings.AppendLine($"{AppStrings.RfMode}: {rfidConfig.RfMode}");
                }
                else if (paramId == UhfParamId.Lbt)
                {
                    settings.AppendLine($"{AppStrings.LBT}: {rfidConfig.Lbt}");
                }
                else if (paramId == UhfParamId.FastId)
                {
                    settings.AppendLine($"{AppStrings.FastId}: {rfidConfig.FastID}");
                }
                else if (paramId == UhfParamId.TagFocus)
                {
                    settings.AppendLine($"{AppStrings.TagFocus}: {rfidConfig.TagFocus}");
                }
                else if (paramId == UhfParamId.PowerSavingMode)
                {
                    settings.AppendLine($"{AppStrings.PowerSavingMode}: {rfidConfig.PowerSavingMode}");
                }
                else if (paramId == UhfParamId.LbtConfigure)
                {
                    settings.AppendLine($"{AppStrings.LbtRssi}: {rfidConfig.LbtCfgSetting}");
                }
                else if (paramId == UhfParamId.AntennaSwitchingMode)
                {
                    settings.AppendLine($"{AppStrings.AntennaSwitchingMode}: {rfidConfig.AntennaSwitchingMode}");
                }
                else if (paramId == UhfParamId.FrequencySwitchingMode)
                {
                    settings.AppendLine($"{AppStrings.FrequencySwitchingMode}: {rfidConfig.FreqSwitchingMode}");
                }
                else if (paramId == UhfParamId.ModuleProfile)
                {
                    settings.AppendLine($"{AppStrings.Profile}: {rfidConfig.ModuleProfile}");
                }
            }

            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
            {
                try
                {
                    settings.AppendLine($"{AppStrings.AutoOffTime}: {baseReader.AutoOffTime}");

                    var screenOff = baseReader.ScreenOffTime;
                    if (screenOff.Minute == 0)
                    {
                        if (screenOff.Second == 0)
                            settings.AppendLine($"{AppStrings.AutoScreenOffTime}: disable");
                        else
                            settings.AppendLine($"{AppStrings.AutoScreenOffTime}: {screenOff.Second} Secs");
                    }
                    else
                    {

                        if (screenOff.Second > 0)
                            settings.AppendLine($"{AppStrings.AutoScreenOffTime}: {screenOff.Minute} Mins {screenOff.Second} Secs");
                        else
                            settings.AppendLine($"{AppStrings.AutoScreenOffTime}: {screenOff.Minute} Mins");
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warn("DevicePage", $"Could not retrieve RP902 specific settings: {ex}");
                }
            }

        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "Error retrieving device settings: " + e.ToString());
        }

        return settings.ToString();
    }

    public void UpdateKeyState(KeyType keyType, KeyState keyState)
    {
        // Update the key state UI elements based on the key type and state
        Log.Info("DevicePage", $"Key event received - Type: {keyType}, State: {keyState}");

        if (keyType == KeyType.Trigger)
        {
            if (keyState == KeyState.KeyDown && baseReader.Action == ActionState.Stop)
            {
                DoInventory();
            }
            else if (keyState == KeyState.KeyUp && baseReader.Action == ActionState.Inventory6c)
            {
                DoStop();
            }
        }
        else if (keyType == KeyType.UpperKey || keyType == KeyType.LowerKey)
        {
            UpdateButtonState(UIElement.InfoButton, AppStrings.Info, keyState == KeyState.KeyUp);
            UpdateButtonState(UIElement.SettingsButton, AppStrings.Settings, keyState == KeyState.KeyUp);
            UpdateButtonState(UIElement.InventoryButton, keyState == KeyState.KeyDown ? AppStrings.Stop : AppStrings.Inventory, true);
            UpdateButtonState(UIElement.CustomInventoryButton, keyState == KeyState.KeyDown ? AppStrings.Stop : AppStrings.CustomInventory, true);
            UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, keyState == KeyState.KeyUp);
            UpdateButtonState(UIElement.ReadButton, AppStrings.Read, keyState == KeyState.KeyUp);
            UpdateButtonState(UIElement.WriteButton, AppStrings.Write, keyState == KeyState.KeyUp);
            UpdateButtonState(UIElement.LockButton, AppStrings.Lock, keyState == KeyState.KeyUp);
            UpdateButtonState(UIElement.UnlockButton, AppStrings.Unlock, keyState == KeyState.KeyUp);

            UpdateSwitchEnable(UIElement.FastIdSwitch, keyState == KeyState.KeyUp);
            UpdateSwitchEnable(UIElement.Gen2xSwitch, keyState == KeyState.KeyUp);
        }
    }

    #region Update UI

    public void UpdateConnectionState(ConnectState connectState)
    {
        _connectState = connectState;
        string connectionText = connectState.ToString();
        Color connectionColor = connectState == ConnectState.Connected ? Colors.Green : Colors.Red;
        UpdateLabelState(UIElement.ConnectStateLabel, connectionText, connectionColor);

        if (connectState == ConnectState.Connected)
        {
            // Additional actions for connected state
            SetUseGunKeyCode();
            SetDataCollectionMode();

            try
            {
                baseReader.RfidUhf.FastID = FastIdSwitch.IsToggled;
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", "Error FastID settings: " + ex.ToString());
            }

            try
            {
                baseReader.RfidUhf.RFMode = Gen2xSwitch.IsToggled ? RFMode.Rf4123 : RFMode.Rf244;
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", "Error RFMode settings: " + ex.ToString());
            }
        }

        EnableFunctions(connectState == ConnectState.Connected);
    }

    void EnableFunctions(bool enable)
    {
        UpdateButtonState(UIElement.InfoButton, AppStrings.Info, enable);
        UpdateButtonState(UIElement.SettingsButton, AppStrings.Settings, enable);
        UpdateButtonState(UIElement.InventoryButton, AppStrings.Inventory, enable);
        UpdateButtonState(UIElement.CustomInventoryButton, AppStrings.CustomInventory, enable);
        UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, enable);
        UpdateButtonState(UIElement.ReadButton, AppStrings.Read, enable);
        UpdateButtonState(UIElement.WriteButton, AppStrings.Write, enable);
        UpdateButtonState(UIElement.LockButton, AppStrings.Lock, enable);
        UpdateButtonState(UIElement.UnlockButton, AppStrings.Unlock, enable);
        UpdateButtonState(UIElement.SendButton, AppStrings.Send, enable);

        if (deviceType != Com.Unitech.Lib.Types.DeviceType.Rp902)
        {
            UpdateContainerVisibility(UIElement.FastIdRow, enable);
        }

        if (deviceType != Com.Unitech.Lib.Types.DeviceType.Rp902 && deviceType != Com.Unitech.Lib.Types.DeviceType.Rg768 &&
            deviceType != Com.Unitech.Lib.Types.DeviceType.Ht730)
        {
            UpdateContainerVisibility(UIElement.Gen2xRow, enable);
        }
    }

    public void UpdateBatteryState(int batteryLevel)
    {
        string batteryText = $"{batteryLevel}%";
        Color batteryColor;

        // Change color based on battery level
        if (batteryLevel > 60)
            batteryColor = Colors.Green;
        else if (batteryLevel > 20)
            batteryColor = Colors.Orange;
        else
            batteryColor = Colors.Red;

        // Using enum-based function
        UpdateLabelState(UIElement.BatteryLabel, batteryText, batteryColor);
    }

    public void UpdateTemperatureState(double temperature)
    {
        string temperatureText = $"{temperature:F1}°C";
        Color temperatureColor;

        // Change color based on temperature (normal range 20-40°C)
        if (temperature <= 40)
            temperatureColor = Colors.Green;
        else if (temperature > 40 && temperature <= 60)
            temperatureColor = Colors.Orange;
        else
            temperatureColor = Colors.Red;

        // Using enum-based function
        UpdateLabelState(UIElement.TemperatureLabel, temperatureText, temperatureColor);
    }

    public void UpdateEpcState(string epc)
    {
        string epcText = string.IsNullOrEmpty(epc) ? "--" : epc;
        Color epcColor = string.IsNullOrEmpty(epc) ? Colors.Gray : Colors.Blue;

        // Using enum-based function
        UpdateLabelState(UIElement.EpcLabel, epcText, epcColor);
    }

    public void UpdateRssiState(string rssi)
    {
        string rssiText = rssi == "0" ? "--" : rssi;
        Color rssiColor = rssi == "0" ? Colors.Gray : Colors.Blue;

        // Using enum-based function
        UpdateLabelState(UIElement.RssiLabel, rssiText, rssiColor);
    }

    public void UpdateTidState(string tid)
    {
        string tidText = string.IsNullOrEmpty(tid) ? "--" : tid;
        Color tidColor = string.IsNullOrEmpty(tid) ? Colors.Gray : Colors.Purple;

        // Using enum-based function
        UpdateLabelState(UIElement.TidLabel, tidText, tidColor);
    }

    public void UpdateSgtinState(string sgtin)
    {
        string sgtinText = string.IsNullOrEmpty(sgtin) ? "--" : sgtin;
        Color sgtinColor = string.IsNullOrEmpty(sgtin) ? Colors.Gray : Colors.DarkGreen;

        // Using enum-based function
        UpdateLabelState(UIElement.SgtinLabel, sgtinText, sgtinColor);
    }

    public void UpdateResultState(string result, bool isSuccess = true)
    {
        string resultText = string.IsNullOrEmpty(result) ? "--" : result;
        Color resultColor = string.IsNullOrEmpty(result) ? Colors.Gray :
                           isSuccess ? Colors.Green : Colors.Red;

        // Using enum-based function
        UpdateLabelState(UIElement.ResultLabel, resultText, resultColor);
    }

    public void UpdateDataState(string data)
    {
        string dataText = string.IsNullOrEmpty(data) ? "--" : data;
        Color dataColor = string.IsNullOrEmpty(data) ? Colors.Gray : Colors.Blue;

        // Using enum-based function
        UpdateLabelState(UIElement.DataLabel, dataText, dataColor);
    }

    public void UpdateTimestampState(DateTime? timestamp = null)
    {
        string timestampText;
        Color timestampColor;

        if (timestamp.HasValue)
        {
            timestampText = timestamp.Value.ToString("HH:mm:ss");
            timestampColor = Colors.DarkBlue;
        }
        else
        {
            timestampText = "--";
            timestampColor = Colors.Gray;
        }

        // Using enum-based function
        UpdateLabelState(UIElement.TimestampLabel, timestampText, timestampColor);
    }

    public void UpdateBarcodeIdState(string barcodeId)
    {
        string barcodeText = string.IsNullOrEmpty(barcodeId) ? "--" : barcodeId;
        Color barcodeColor = string.IsNullOrEmpty(barcodeId) ? Colors.Gray : Colors.Orange;

        // Using enum-based function
        UpdateLabelState(UIElement.BarcodeIdLabel, barcodeText, barcodeColor);
    }

    public void UpdateBarcodeDataState(string barcodeData)
    {
        string dataText = string.IsNullOrEmpty(barcodeData) ? "--" : barcodeData;
        Color dataColor = string.IsNullOrEmpty(barcodeData) ? Colors.Gray : Colors.Purple;

        // Using enum-based function
        UpdateLabelState(UIElement.BarcodeDataLabel, dataText, dataColor);
    }

    private void InitUI()
    {
        if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
        {
            UpdateContainerVisibility(UIElement.FastIdRow, false);
            UpdateContainerVisibility(UIElement.TidRow, false);

            UpdateContainerVisibility(UIElement.DisplayTextContainer, true);
        }
        else
        {
            UpdateContainerVisibility(UIElement.FastIdRow, true);
            UpdateContainerVisibility(UIElement.TidRow, true);

            UpdateContainerVisibility(UIElement.DisplayTextContainer, false);
        }

        if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902 || deviceType == Com.Unitech.Lib.Types.DeviceType.Rg768 || deviceType == Com.Unitech.Lib.Types.DeviceType.Ht730)
        {
            UpdateContainerVisibility(UIElement.Gen2xRow, false);
        }
        else
        {
            UpdateContainerVisibility(UIElement.Gen2xRow, true);
        }

        if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp300)
        {
            UpdateContainerVisibility(UIElement.BarcodeIdRow, true);
            UpdateContainerVisibility(UIElement.BarcodeDataRow, true);
        }
        else
        {
            UpdateContainerVisibility(UIElement.BarcodeIdRow, false);
            UpdateContainerVisibility(UIElement.BarcodeDataRow, false);
        }

        if (deviceType != Com.Unitech.Lib.Types.DeviceType.Pa768e
            && deviceType != Com.Unitech.Lib.Types.DeviceType.Rp300
            && deviceType != Com.Unitech.Lib.Types.DeviceType.Ea530)
        {
            UpdateButtonVisibility(UIElement.CustomInventoryButton, false);
        }
        else
        {
            UpdateButtonVisibility(UIElement.CustomInventoryButton, true);
        }
    }

    private void ResetLabels()
    {
        UpdateLabelState(UIElement.BatteryLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.TemperatureLabel, "--°C", Colors.Gray);
        UpdateLabelState(UIElement.EpcLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.RssiLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.TidLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.SgtinLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.ResultLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.DataLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.TimestampLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.BarcodeIdLabel, "--", Colors.Gray);
        UpdateLabelState(UIElement.BarcodeDataLabel, "--", Colors.Gray);
    }

    public void UpdateActionState(ActionState actionState)
    {
        UpdateButtonState(UIElement.InfoButton, AppStrings.Info, actionState == ActionState.Stop);
        UpdateButtonState(UIElement.SettingsButton, AppStrings.Settings, actionState == ActionState.Stop);
        UpdateButtonState(UIElement.ReadButton, AppStrings.Read, actionState == ActionState.Stop);
        UpdateButtonState(UIElement.WriteButton, AppStrings.Write, actionState == ActionState.Stop);
        UpdateButtonState(UIElement.LockButton, AppStrings.Lock, actionState == ActionState.Stop);
        UpdateButtonState(UIElement.UnlockButton, AppStrings.Unlock, actionState == ActionState.Stop);
        UpdateButtonState(UIElement.SendButton, AppStrings.Send, actionState == ActionState.Stop);

        if (actionState == ActionState.Inventory6c)
        {
            if (_isFindTag)
            {
                UpdateButtonState(UIElement.FindTagButton, AppStrings.Stop, true);
                UpdateButtonState(UIElement.InventoryButton, AppStrings.Inventory, false);
                UpdateButtonState(UIElement.CustomInventoryButton, AppStrings.CustomInventory, false);
            }
            else
            {
                UpdateButtonState(UIElement.InventoryButton, AppStrings.Stop, true);
                UpdateButtonState(UIElement.CustomInventoryButton, AppStrings.Stop, true);

                UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, false);

            }
        }
        else if (actionState == ActionState.Stop)
        {
            UpdateButtonState(UIElement.InventoryButton, AppStrings.Inventory, true);
            UpdateButtonState(UIElement.CustomInventoryButton, AppStrings.CustomInventory, true);
            UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, true);
        }

    }

    #region Label State Updates

    public void UpdateLabelText(UIElement label, string text)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                Label labelControl = label switch
                {
                    UIElement.ConnectStateLabel => ConnectStateLabel,
                    UIElement.BatteryLabel => BatteryLabel,
                    UIElement.TemperatureLabel => TemperatureLabel,
                    UIElement.EpcLabel => EpcLabel,
                    UIElement.RssiLabel => RssiLabel,
                    UIElement.TidLabel => TidLabel,
                    UIElement.SgtinLabel => SgtinLabel,
                    UIElement.ResultLabel => ResultLabel,
                    UIElement.DataLabel => DataLabel,
                    UIElement.TimestampLabel => TimestampLabel,
                    UIElement.BarcodeIdLabel => BarcodeIdLabel,
                    UIElement.BarcodeDataLabel => BarcodeDataLabel,
                    _ => null
                };

                if (labelControl != null)
                {
                    labelControl.Text = text;
                }
                else
                {
                    Log.Debug("DevicePage", $"UI element '{label}' is not a label or unknown element");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating label '{label}': {ex}");
            }
        });
    }

    public void UpdateLabelColor(UIElement label, Color textColor)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                Label labelControl = label switch
                {
                    UIElement.ConnectStateLabel => ConnectStateLabel,
                    UIElement.BatteryLabel => BatteryLabel,
                    UIElement.TemperatureLabel => TemperatureLabel,
                    UIElement.EpcLabel => EpcLabel,
                    UIElement.RssiLabel => RssiLabel,
                    UIElement.TidLabel => TidLabel,
                    UIElement.SgtinLabel => SgtinLabel,
                    UIElement.ResultLabel => ResultLabel,
                    UIElement.DataLabel => DataLabel,
                    UIElement.TimestampLabel => TimestampLabel,
                    UIElement.BarcodeIdLabel => BarcodeIdLabel,
                    UIElement.BarcodeDataLabel => BarcodeDataLabel,
                    _ => null
                };

                if (labelControl != null)
                {
                    labelControl.TextColor = textColor;
                }
                else
                {
                    Log.Debug("DevicePage", $"UI element '{label}' is not a label or unknown element");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating label '{label}': {ex}");
            }
        });
    }

    public void UpdateLabelVisibility(UIElement label, bool isVisible)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                Label labelControl = label switch
                {
                    UIElement.ConnectStateLabel => ConnectStateLabel,
                    UIElement.BatteryLabel => BatteryLabel,
                    UIElement.TemperatureLabel => TemperatureLabel,
                    UIElement.EpcLabel => EpcLabel,
                    UIElement.RssiLabel => RssiLabel,
                    UIElement.TidLabel => TidLabel,
                    UIElement.SgtinLabel => SgtinLabel,
                    UIElement.ResultLabel => ResultLabel,
                    UIElement.DataLabel => DataLabel,
                    UIElement.TimestampLabel => TimestampLabel,
                    UIElement.BarcodeIdLabel => BarcodeIdLabel,
                    UIElement.BarcodeDataLabel => BarcodeDataLabel,
                    _ => null
                };

                if (labelControl != null)
                {
                    labelControl.IsVisible = isVisible;
                }
                else
                {
                    Log.Debug("DevicePage", $"UI element '{label}' is not a label or unknown element");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating label visibility '{label}': {ex}");
            }
        });
    }

    public void UpdateLabelState(UIElement label, string text, Color textColor = default)
    {
        UpdateLabelText(label, text);
        UpdateLabelColor(label, textColor);
    }

    #endregion

    #region Button State Updates

    public void UpdateButtonText(UIElement button, string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Microsoft.Maui.Controls.Button buttonControl = button switch
                {
                    UIElement.InfoButton => InfoBtn,
                    UIElement.SettingsButton => SettingsBtn,
                    UIElement.InventoryButton => InventoryBtn,
                    UIElement.CustomInventoryButton => CustomInventoryBtn,
                    UIElement.FindTagButton => FindTagBtn,
                    UIElement.ReadButton => ReadBtn,
                    UIElement.WriteButton => WriteBtn,
                    UIElement.LockButton => LockBtn,
                    UIElement.UnlockButton => UnlockBtn,
                    UIElement.SendButton => SendBtn,
                    _ => null
                };

                if (buttonControl != null)
                {
                    buttonControl.Text = text;
                }
                else
                {
                    Log.Debug("DevicePage", $"UI element '{button}' is not a button or unknown button");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating button '{button}': {ex}");
            }
        });
    }

    public void UpdateButtonEnable(UIElement button, bool isEnabled)
    {
        // Use MainThread directly instead of nested threading
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Microsoft.Maui.Controls.Button buttonControl = button switch
                {
                    UIElement.InfoButton => InfoBtn,
                    UIElement.SettingsButton => SettingsBtn,
                    UIElement.InventoryButton => InventoryBtn,
                    UIElement.CustomInventoryButton => CustomInventoryBtn,
                    UIElement.FindTagButton => FindTagBtn,
                    UIElement.ReadButton => ReadBtn,
                    UIElement.WriteButton => WriteBtn,
                    UIElement.LockButton => LockBtn,
                    UIElement.UnlockButton => UnlockBtn,
                    UIElement.SendButton => SendBtn,
                    _ => null
                };

                if (buttonControl != null)
                {
                    // Use Color.Parse instead of Color.FromArgb for better reliability
                    Color originalColor = button switch
                    {
                        UIElement.InfoButton => Color.Parse("#2196F3"),
                        UIElement.SettingsButton => Color.Parse("#FF9800"),
                        UIElement.InventoryButton => Color.Parse("#4CAF50"),
                        UIElement.CustomInventoryButton => Color.Parse("#FF6B35"),
                        UIElement.FindTagButton => Color.Parse("#9C27B0"),
                        UIElement.ReadButton => Color.Parse("#03A9F4"),
                        UIElement.WriteButton => Color.Parse("#E91E63"),
                        UIElement.LockButton => Color.Parse("#F44336"),
                        UIElement.UnlockButton => Color.Parse("#8BC34A"),
                        UIElement.SendButton => Color.Parse("#673AB7"),
                        _ => Colors.Blue
                    };

                    // Change button color based on enabled state
                    if (!isEnabled)
                    {
                        buttonControl.Background = Colors.Gray;
                    }
                    else
                    {
                        buttonControl.Background = originalColor;
                    }

                    buttonControl.IsEnabled = isEnabled;
                }
                else
                {
                    Log.Error("DevicePage", $"UI element '{button}' is not a button or unknown button");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating button '{button}': {ex}");
            }
        });
    }

    public void UpdateButtonVisibility(UIElement button, bool isVisible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Microsoft.Maui.Controls.Button buttonControl = button switch
                {
                    UIElement.InfoButton => InfoBtn,
                    UIElement.SettingsButton => SettingsBtn,
                    UIElement.InventoryButton => InventoryBtn,
                    UIElement.CustomInventoryButton => CustomInventoryBtn,
                    UIElement.FindTagButton => FindTagBtn,
                    UIElement.ReadButton => ReadBtn,
                    UIElement.WriteButton => WriteBtn,
                    UIElement.LockButton => LockBtn,
                    UIElement.UnlockButton => UnlockBtn,
                    UIElement.SendButton => SendBtn,
                    _ => null
                };

                if (buttonControl != null)
                {
                    buttonControl.IsVisible = isVisible;
                }
                else
                {
                    Log.Error("DevicePage", $"UI element '{button}' is not a button or unknown button");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating button visibility '{button}': {ex}");
            }
        });
    }

    /// <summary>
    /// Update button state (text and enabled status)
    /// </summary>
    /// <param name="button">Button element to update</param>
    /// <param name="text">Button text</param>
    /// <param name="isEnabled">Whether button is enabled</param>
    public void UpdateButtonState(UIElement button, string text, bool isEnabled)
    {
        UpdateButtonEnable(button, isEnabled);
        UpdateButtonText(button, text);
        // UpdateButtonColor(button, backgroundColor);
    }

    #endregion

    /// <summary>
    /// Update switch state (toggled status)
    /// </summary>
    /// <param name="switchElement">Switch element to update</param>
    /// <param name="isEnabled">Whether switch is enabled</param>
    public void UpdateSwitchEnable(UIElement switchElement, bool isEnabled)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                Microsoft.Maui.Controls.Switch switchControl = switchElement switch
                {
                    UIElement.FastIdSwitch => FastIdSwitch,
                    UIElement.Gen2xSwitch => Gen2xSwitch,
                    _ => null
                };

                if (switchControl != null)
                {
                    switchControl.IsEnabled = isEnabled;
                }
                else
                { 
                    Log.Error("DevicePage", $"UI element '{switchElement}' is not a switch or unknown switch");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating switch '{switchElement}': {ex}");
            }
        });
    }

    /// <summary>
    /// Update container/row visibility using UIElement enum
    /// </summary>
    /// <param name="container">Container element to update</param>
    /// <param name="isVisible">Whether the container should be visible</param>
    public void UpdateContainerVisibility(UIElement container, bool isVisible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                View containerControl = container switch
                {
                    UIElement.TidRow => TidRow,
                    UIElement.BarcodeIdRow => BarcodeIdRow,
                    UIElement.BarcodeDataRow => BarcodeDataRow,
                    UIElement.FastIdRow => FastIdRow,
                    UIElement.Gen2xRow => Gen2xRow,
                    UIElement.DisplayTextContainer => DisplayTextFrame,
                    _ => null
                };

                if (containerControl != null)
                {
                    containerControl.IsVisible = isVisible;
                    Log.Debug("DevicePage", $"Container '{container}' visibility updated to: {isVisible}");
                }
                else
                {
                    Log.Error("DevicePage", $"UI element '{container}' is not a container or unknown container");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating container visibility '{container}': {ex}");
            }
        });
    }

    /// <summary>
    /// Update progress bar and percentage label
    /// </summary>
    /// <param name="progress">Progress value between 0.0 and 1.0</param>
    public void UpdateProgress(double progress)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                // Ensure progress is within valid range
                progress = System.Math.Max(0.0, System.Math.Min(1.0, progress));

                // Update progress bar
                ProgressBar.Progress = progress;
                ProgressBar.IsVisible = true;

                // Change progress bar color based on value
                Color progressColor;
                if (progress < 0.3) // Low progress (0-30%) - Red
                {
                    progressColor = Colors.Red;
                }
                else if (progress < 0.7) // Medium progress (30-70%) - Orange
                {
                    progressColor = Colors.Orange;
                }
                else // High progress (70-100%) - Green
                {
                    progressColor = Colors.Green;
                }
                ProgressBar.ProgressColor = progressColor;
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", $"Error updating progress: {ex}");
            }
        });
    }

    /// <summary>
    /// Reset progress bar to initial state
    /// </summary>
    public void ResetProgress()
    {
        UpdateProgress(0.0);
    }

    void ClearResult()
    {
        UpdateLabelText(UIElement.ResultLabel, "");
        UpdateLabelText(UIElement.DataLabel, "");
    }

    #endregion

    private void ConnectTask()
    {
        if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
        {
            try
            {
                TransportBluetooth tb = new TransportBluetooth(Com.Unitech.Lib.Types.DeviceType.Rp902, "RP902", bluetoothMacAddress);
                baseReader = new RP902Reader(tb);
                baseReader.AddListener(readerEventHandler);
                baseReader.SetRfidEventListener(rfidEventHandler);
                baseReader.Connect();
            }
            catch (Exception ex)
            {
                Log.Error("DevicePage", "Error connecting to RP902: " + ex.ToString());
            }
        }
        else if (deviceType == Com.Unitech.Lib.Types.DeviceType.Ht730)
        {
            try
            {
                baseReader = new HT730Reader(Android.App.Application.Context);
                baseReader.AddListener(readerEventHandler);
                baseReader.SetRfidEventListener(rfidEventHandler);

                baseReader.Connect();
            }
            catch (Exception ex)
            {
                Log.Error("DevicePage", "Error connecting to HT730: " + ex.ToString());
            }
        }
        else if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rg768)
        {
            try
            {
                baseReader = new RG768Reader(Android.App.Application.Context);
                baseReader.AddListener(readerEventHandler);
                baseReader.SetRfidEventListener(rfidEventHandler);

                baseReader.Connect();
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", "Error connecting to RG768: " + ex.ToString());
            }
        }
        else if (deviceType == Com.Unitech.Lib.Types.DeviceType.Pa768e)
        {
            try
            {
                baseReader = new PA768eReader(Android.App.Application.Context);
                baseReader.AddListener(readerEventHandler);
                baseReader.SetRfidEventListener(rfidEventHandler);

                // Attempt to connect
                _ = baseReader.Connect();
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", "Error connecting to PA768e: " + ex.ToString());

            }
        }
        else if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp300)
        {
            if (isusb)
            {
                if (detectReader != null)
                {
                    detectReader.Stop();
                    detectReader = null;
                }

                detectReader = new DetectReader(Android.App.Application.Context, usbEventHandler);
                detectReader.Start();
            }
            else
            {
                try
                {
                    TransportBluetooth tb = new TransportBluetooth(Com.Unitech.Lib.Types.DeviceType.Rp300, "RP300", bluetoothMacAddress);
                    baseReader = new RP300Reader(tb, Android.App.Application.Context);
                    baseReader.AddListener(readerEventHandler);
                    baseReader.SetRfidEventListener(rfidEventHandler);
                    baseReader.SetBarcodeEventListener(barcodeEventHandler);

                    // Attempt to connect
                    _ = baseReader.Connect();
                }
                catch (System.Exception ex)
                {
                    Log.Error("DevicePage", "Error connecting to RP300: " + ex.ToString());
                }
            }
        }
        else if (deviceType == Com.Unitech.Lib.Types.DeviceType.Ea530)
        {
            try
            {
                baseReader = new EA530Reader(Android.App.Application.Context);
                baseReader.AddListener(readerEventHandler);
                baseReader.SetRfidEventListener(rfidEventHandler);

                // Attempt to connect
                _ = baseReader.Connect();
            }
            catch (System.Exception ex)
            {
                Log.Error("DevicePage", "Error connecting to EA530: " + ex.ToString());
            }
        }
        else
        {
            // For other device types, show as not implemented
            Log.Debug("DevicePage", $"Connection not implemented for device type: {deviceType}");
        }
    }

    void AssertReader()
    {
        if (baseReader == null)
        {
            throw new Java.Lang.Exception("Reader is not initialized. Please connect to a reader first.");
        }
        else if (baseReader.State != ConnectState.Connected)
        {
            throw new Java.Lang.Exception("Reader is not connected. Please connect to a reader first.");
        }
    }

    void InitSetting()
    {
        try
        {
            RFIDConfig rfidConfig = baseReader.RfidUhf.Config;


            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
                rfidConfig.Power = 22; // Default power for RP902
            else
                rfidConfig.Power = 28;

            rfidConfig.ContinuousMode = true; // Default continuous mode

            rfidConfig.InventoryTime = 200; // Default inventory time in milliseconds

            rfidConfig.IdleTime = 20; // Default idle time in milliseconds

            rfidConfig.Algorithm = AlgorithmType.DynamicQ; // Default algorithm

            rfidConfig.StartQ = 4; // Default StartQ

            rfidConfig.MaxQ = 15; // Default MaxQ

            rfidConfig.MinQ = 0; // Default MinQ

            rfidConfig.Target = Target.A; // Default target

            rfidConfig.Session = Session.S0; // Default session

            rfidConfig.SelectMode = SelectMode.All; // Default select mode

            rfidConfig.Encoding = Encoding.Fm0;

            rfidConfig.TariType = TARIType.T2500; // Default Tari

            rfidConfig.BlfType = BLFType.Blf256; // Default BLF

            rfidConfig.ToggleTarget = true; // Default toggle target

            rfidConfig.FastMode = false; // Default fast mode

            rfidConfig.DwellTime = 1000; // Default dwell time in milliseconds

            //rfidConfig.RfMode = Gen2xSwitch.IsToggled ? RFMode.Rf4123:RFMode.Rf244; // Default RF mode

            rfidConfig.Lbt = false; // Default LBT setting

            rfidConfig.FastID = FastIdSwitch.IsToggled; // Default FastID setting

            rfidConfig.TagFocus = false; // Default tag focus

            rfidConfig.PowerSavingMode = PowerSavingMode.Saving; // Default power saving mode

            rfidConfig.LbtCfgSetting = -74; // Default LBT RSSI setting

            rfidConfig.AntennaSwitchingMode = AntennaSwitchingMode.Stop; // Default antenna switching mode

            rfidConfig.FreqSwitchingMode = FreqSwitchingMode.Stop; // Default frequency switching mode

            rfidConfig.ModuleProfile = 0; // Default module profile

            Log.Debug("DevicePage", $"Setup RFID Config: {rfidConfig}");

            baseReader.RfidUhf.Config = rfidConfig;
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "Error retrieving device settings: " + e.ToString());
        }
    }

    bool SetSelectMask(string maskEpc)
    {
        SelectMask6cParam param = new SelectMask6cParam(
                true,
                Mask6cTarget.Sl,
                Mask6cAction.Ab,
                BankType.Epc,
                0,
                maskEpc,
                maskEpc.Length * NIBLE_SIZE);
        try
        {
            for (int i = 0; i < baseReader.RfidUhf.SelectMask6cMaxSize; i++)
            {
                baseReader.RfidUhf.SetSelectMask6cEnabled(i, false);
            }
            baseReader.RfidUhf.SetSelectMask6c(0, param);

            baseReader.RfidUhf.SelectMode = SelectMode.Selected;
            Log.Debug("DevicePage", "Select mask set: " + param.ToString());
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "SetSelectMask failed: " + e.ToString());
            Toast.MakeText(Android.App.Application.Context, "SetSelectMask failed: " + e.ToString(), ToastLength.Long).Show();
            return false;
        }
        return true;
    }

    void ClearSelectMask()
    {

        for (int i = 0; i < baseReader.RfidUhf.SelectMask6cMaxSize; i++)
        {
            try
            {
                baseReader.RfidUhf.SetSelectMask6cEnabled(i, false);
            }
            catch (ReaderException e)
            {
                throw e;
            }
        }

        Log.Debug("DevicePage", "Select mask cleared");

        try
        {
            baseReader.RfidUhf.SelectMode = SelectMode.All;
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "clearSelectMask failed: " + e.ToString());
            Toast.MakeText(Android.App.Application.Context, "clearSelectMask failed: " + e.ToString(), ToastLength.Long).Show();
            throw e;
        }

    }

    void DoInventory()
    {
        try
        {
            if (_connectState != ConnectState.Connected) return;

            InitSetting();

            ClearSelectMask();

            _isFindTag = false;
            UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, false);

            if (baseReader.DeviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
            {
                baseReader.SetDisplayTags(new DisplayTags(ReadOnceState.Off, BeepAndVibrateState.On));
            }

            EpcLabel.Text = "";
            RssiLabel.Text = "";
            TidLabel.Text = "";
            SgtinLabel.Text = "";
            baseReader.RfidUhf.Inventory6c();

            UpdateSwitchEnable(UIElement.FastIdSwitch, false);
            UpdateSwitchEnable(UIElement.Gen2xSwitch, false);
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "Inventory error: " + e.ToString());
            Toast.MakeText(Android.App.Application.Context, "Inventory error: " + e.ToString(), ToastLength.Long).Show();
            UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, true);
            UpdateSwitchEnable(UIElement.FastIdSwitch, true);
            UpdateSwitchEnable(UIElement.Gen2xSwitch, true);
        }
    }

    void DoCustomInventory()
    {
        try
        {
            if (_connectState != ConnectState.Connected) return;

            InitSetting();

            ClearSelectMask();

            _isFindTag = false;
            UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, false);

            if (baseReader.DeviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
            {
                baseReader.SetDisplayTags(new DisplayTags(ReadOnceState.Off, BeepAndVibrateState.On));
            }

            EpcLabel.Text = "";
            RssiLabel.Text = "";
            TidLabel.Text = "";
            SgtinLabel.Text = "";
            baseReader.RfidUhf.UserInventory6c(BankType.Tid, 0, 6, "00000000");

            UpdateSwitchEnable(UIElement.FastIdSwitch, false);
            UpdateSwitchEnable(UIElement.Gen2xSwitch, false);
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "Custom Inventory error: " + e.ToString());
            Toast.MakeText(Android.App.Application.Context, "Custom Inventory error: " + e.ToString(), ToastLength.Long).Show();
            UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, true);
            UpdateSwitchEnable(UIElement.FastIdSwitch, true);
            UpdateSwitchEnable(UIElement.Gen2xSwitch, true);
        }
    }

    void DoRead()
    {
        string targetTag = EpcLabel.Text.Trim();

        if (string.IsNullOrEmpty(targetTag) || !System.Text.RegularExpressions.Regex.IsMatch(targetTag, @"^[0-9A-Fa-f]+$"))
        {
            Log.Error("DevicePage", "EPC is empty");
            Toast.MakeText(Android.App.Application.Context, "EPC is empty", ToastLength.Long).Show();
            return;
        }

        try
        {
            if (SetSelectMask(targetTag))
            {
                baseReader.RfidUhf.ReadMemory6c(BankType.Epc, 2, 6, "00000000");
            }
        }
        catch (Java.Lang.Exception e)
        {
            Log.Error("DevicePage", "Read error: " + e.ToString());
            Toast.MakeText(Android.App.Application.Context, "Read error: " + e.ToString(), ToastLength.Long).Show();
        }
    }

    void DoWrite()
    {
        string targetTag = EpcLabel.Text.Trim();

        if (string.IsNullOrEmpty(targetTag) || !System.Text.RegularExpressions.Regex.IsMatch(targetTag, @"^[0-9A-Fa-f]+$"))
        {
            Log.Error("DevicePage", "EPC is empty");
            Toast.MakeText(Android.App.Application.Context, "EPC is empty", ToastLength.Long).Show();
            return;
        }

        try
        {
            if (SetSelectMask(targetTag))
            {
                if (targetTag.StartsWith("1234"))
                {
                    targetTag = "4321" + targetTag.Substring(4);
                }
                else
                {
                    targetTag = "1234" + targetTag.Substring(4);
                }

                baseReader.RfidUhf.WriteMemory6c(BankType.Epc, 2, targetTag, "00000000");
            }
        }
        catch (Java.Lang.Exception e)
        {
            Log.Error("DevicePage", "Write error: " + e.ToString());
            Toast.MakeText(Android.App.Application.Context, "Write error: " + e.ToString(), ToastLength.Long).Show();
        }
    }

    void LockUnlockProc(bool isLock)
    {
        string targetTag = EpcLabel.Text.Trim();

        if (string.IsNullOrEmpty(targetTag) || !System.Text.RegularExpressions.Regex.IsMatch(targetTag, @"^[0-9A-Fa-f]+$"))
        {
            Log.Error("DevicePage", "EPC is empty");
            Toast.MakeText(Android.App.Application.Context, "EPC is empty", ToastLength.Long).Show();
            return;
        }

        try
        {
            if (SetSelectMask(targetTag))
            {
                string accessPassword = "00000000";
                string data = "12345678";
                int offset = 2;

                accessTagResult = false;

                // Write access password
                ResultCode resultCode = baseReader.RfidUhf.WriteMemory6c(BankType.Reserved, offset, data, accessPassword);

                if (resultCode != ResultCode.NoError)
                {
                    Log.Error("DevicePage", "Write access password failed: " + resultCode);
                    return;
                }

                TimeoutControl timeoutControl = new TimeoutControl(3000);

                while (baseReader.Action != ActionState.Stop)
                {
                    if (timeoutControl.IsTimeout)
                    {
                        Log.Error("DevicePage", "Write password timeout");
                        return;
                    }
                    System.Threading.Thread.Sleep(100);
                }

                if (!accessTagResult)
                {
                    Log.Error("DevicePage", "Write password fail from access result.");
                    return;
                }

                accessPassword = data;

                Lock6cParam lockParam = new Lock6cParam();
                lockParam.Epc = isLock ? LockState.Lock : LockState.Unlock;

                resultCode = baseReader.RfidUhf.Lock6c(lockParam, accessPassword);

                if (resultCode != ResultCode.NoError)
                {
                    Log.Error("DevicePage", $"{(isLock ? "Lock" : "Unlock")} failed: " + resultCode);
                }
            }
        }
        catch (ReaderException e)
        {
            string operation = isLock ? "Lock" : "Unlock";
            Log.Error("DevicePage", $"{operation} ReaderException: " + e.ToString());

            // Update UI on main thread
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                UpdateResultState($"{operation} Failed: " + e.ToString(), false);
            });
        }
        catch (Java.Lang.Exception e)
        {
            string operation = isLock ? "Lock" : "Unlock";
            Log.Error("DevicePage", $"{operation} error: " + e.ToString());

            // Update UI on main thread
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                UpdateResultState($"{operation} Failed: " + e.ToString(), false);
            });
        }
    }

    void DoStop()
    {
        _isFindTag = false;
        UpdateButtonState(UIElement.FindTagButton, AppStrings.FindTag, true);
        UpdateButtonState(UIElement.InventoryButton, AppStrings.Inventory, true);
        UpdateButtonState(UIElement.CustomInventoryButton, AppStrings.CustomInventory, true);

        // Stop inventory process
        baseReader.RfidUhf.Stop();

        UpdateSwitchEnable(UIElement.FastIdSwitch, true);
        UpdateSwitchEnable(UIElement.Gen2xSwitch, true);
    }

    void DoFind()
    {
        // Get label 
        string targetTag = EpcLabel.Text.Trim();

        if (string.IsNullOrEmpty(targetTag) || !System.Text.RegularExpressions.Regex.IsMatch(targetTag, @"^[0-9A-Fa-f]+$"))
        {
            Log.Error("DevicePage", "EPC is empty");
            Toast.MakeText(Android.App.Application.Context, "EPC is empty", ToastLength.Long).Show();
            return;
        }

        if (SetSelectMask(targetTag))
        {
            _isFindTag = true;
            UpdateButtonState(UIElement.InventoryButton, AppStrings.Inventory, false);
            UpdateButtonState(UIElement.CustomInventoryButton, AppStrings.CustomInventory, false);
            UpdateSwitchEnable(UIElement.FastIdSwitch, false);
            UpdateSwitchEnable(UIElement.Gen2xSwitch, false);

            // Clear progress bar and labels
            ResetProgress();

            if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rp902)
            {
                baseReader.SetDisplayTags(new DisplayTags(ReadOnceState.Off, BeepAndVibrateState.On));
            }
            baseReader.RfidUhf.Inventory6c();
        }
    }

    private void SetDisplayOutput(int pLine, bool bClear, string data)
    {
        const int MAX_CHARS = 16;

        DisplayOutput display = new DisplayOutput();

        sbyte charPosition = 0;
        if (data.Length < MAX_CHARS)
        {
            charPosition = (sbyte)((MAX_CHARS - data.Length) / 2); // Center alignment
        }

        display.CharPosition = charPosition;        // Set calculated alignment
        display.LinePosition = (sbyte)pLine;        // Line 1
        display.ClearScreen = bClear;      // Clear screen first
        display.Message = data; // Text to display

        try
        {
            baseReader.SetDisplayOutput(display);
        }
        catch (ReaderException e)
        {
            Log.Error("DevicePage", "SetDisplayOutput error: " + e.ToString());
        }
    }

    public void ReceiveKeyChange(KeyType keyType, KeyState keyState)
    {
        try
        {
            Log.Debug("DevicePage", $"Key change received: {keyType} - {keyState}");

            if (keyType == KeyType.Trigger)
            {
                if (keyState == KeyState.KeyDown && baseReader.Action == ActionState.Stop)
                {
                    // Trigger pressed, start inventory
                    DoInventory();
                }
                else if (keyState == KeyState.KeyUp && baseReader.Action == ActionState.Inventory6c)
                {
                    // Trigger released, stop inventory
                    DoStop();
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error handling key change: {ex}");
        }
    }

    public void ReceiveTag(string epc, TagExtParam tagExtParam)
    {
        UpdateEpcState(epc);

        if (tagExtParam != null)
        {
            if (tagExtParam.IsContains(TagInfoId.Tid))
            {
                UpdateTidState(tagExtParam.TID);
            }
            else
            {
                UpdateTidState("--");
            }

            if (tagExtParam.IsContains(TagInfoId.Timestamp))
            {
                var javaTime = tagExtParam.Timestamp();
                var dateTime = javaTime != null ?
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(javaTime.Time).ToLocalTime() : (DateTime?)null;

                UpdateTimestampState(dateTime);
            }
            else
            {
                UpdateTimestampState();
            }

            if (tagExtParam.IsContains(TagInfoId.UserData))
            {
                UpdateDataState(tagExtParam.UserData);
            }
            else
            {
                UpdateDataState("");
            }

            float rssi = 0;
            if (tagExtParam.IsContains(TagInfoId.Rssi))
            {
                // Convert Rssi to string and update UI
                rssi = tagExtParam.Rssi;
            }

            SGTIN sGTIN = new SGTIN(epc);
            string gtin14 = sGTIN.GTIN14;
            string serial = sGTIN.Serial;

            if (string.IsNullOrEmpty(gtin14) || string.IsNullOrEmpty(serial))
            {
                UpdateSgtinState(AppStrings.NoSGTIN);
            }
            else
            {
                UpdateSgtinState($"(01){gtin14}(21){serial}");
            }

            if (_isFindTag)
            {
                double size = (double)((rssi - histogramMin) / (histogramMax - histogramMin));

                UpdateProgress(size);
            }

            UpdateRssiState(rssi.ToString());
        }
    }

    public void ReceiveTagAccessResult(string epc, string data, ActionState action, ResultCode code)
    {
        if (code == ResultCode.NoError)
        {
            UpdateResultState(AppStrings.Success, true);
        }
        else
        {
            UpdateResultState(code.ToString(), false);
        }

        UpdateDataState(data);

        accessTagResult = (code == ResultCode.NoError);
    }

    public void ReceiveBarcode(BarcodeEventArgs barcodeEventArgs)
    {
        string barcodeId = "";
        string barcodeData = "";

        if (barcodeEventArgs.IsContains(BarcodeInfoId.BarcodeId))
        {
            barcodeId = barcodeEventArgs.BarcodeId.ToString();
            UpdateBarcodeIdState(barcodeId);
        }
        else
        {
            UpdateBarcodeIdState("");
        }

        if (barcodeEventArgs.IsContains(BarcodeInfoId.Data))
        {
            // Assuming GetData() returns byte[]
            byte[] barcodeBytes = barcodeEventArgs.GetData();
            if (barcodeBytes != null && barcodeBytes.Length > 0)
            {
                barcodeData = System.Text.Encoding.UTF8.GetString(barcodeBytes);
            }
            else
            {
                barcodeData = "";
            }
            UpdateBarcodeDataState(barcodeData);
        }
        else
        {
            UpdateBarcodeDataState("");
        }

        if (barcodeEventArgs.IsContains(BarcodeInfoId.Timestamp))
        {
            var javaTime = barcodeEventArgs.Timestamp;
            var dateTime = javaTime != null ?
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(javaTime.Time).ToLocalTime() : (DateTime?)null;

            UpdateTimestampState(dateTime);
        }
        else
        {
            UpdateTimestampState();
        }
    }

    public void OnUsbAttached(UsbDevice usbDevice)
    {
        UsbManager usbManager = (UsbManager)Android.App.Application.Context.GetSystemService(Context.UsbService);

        if (usbManager.HasPermission(usbDevice))
        {
            // USB device is already connected, handle it
            Log.Debug("DevicePage", "USB device already has permission: " + usbDevice.DeviceName);
            if (baseReader == null)
            {
                TransportUsb usbTransport = new TransportUsb(Com.Unitech.Lib.Types.DeviceType.Rp300, usbDevice);
                baseReader = new RP300Reader(usbTransport, Android.App.Application.Context);
                baseReader.AddListener(readerEventHandler);
                baseReader.SetRfidEventListener(rfidEventHandler);
                baseReader.SetBarcodeEventListener(barcodeEventHandler);
                baseReader.Connect();
            }
        }
    }

    public void OnUsbDetached(UsbDevice usbDevice)
    {
        // Handle USB detached event
        if (baseReader != null)
        {
            baseReader.Disconnect();
            baseReader = null;
        }
    }

    private void EnableReceiver(bool enable)
    {
        if (enable)
        {
            // Check if receiver is already registered
            if (isReceiverRegistered)
            {
                Log.Debug("DevicePage", "Broadcast receiver is already registered, skipping registration");
                return;
            }

            if (mReceiver == null)
            {
                // Create the broadcast receiver
                mReceiver = new RfidBroadcastReceiver(this);
            }

            IntentFilter filter = new IntentFilter();
            filter.AddAction(rfidGunPressed);
            filter.AddAction(rfidGunReleased);
            filter.AddAction(systemExtendedPort);

            try
            {
                // Register receiver using standard API
                Android.App.Application.Context.RegisterReceiver(mReceiver, filter, ReceiverFlags.Exported);
                isReceiverRegistered = true;
                Log.Debug("DevicePage", "Broadcast receiver registered successfully");
            }
            catch (System.Exception e)
            {
                Log.Error("DevicePage", $"Failed to register receiver: {e}");
                isReceiverRegistered = false;
            }
        }
        else
        {
            // Check if receiver is already unregistered
            if (!isReceiverRegistered)
            {
                Log.Debug("DevicePage", "Broadcast receiver is already unregistered, skipping unregistration");
                return;
            }

            if (mReceiver != null)
            {
                try
                {
                    Android.App.Application.Context.UnregisterReceiver(mReceiver);
                    isReceiverRegistered = false;
                    Log.Debug("DevicePage", "Broadcast receiver unregistered successfully");
                }
                catch (System.Exception e)
                {
                    Log.Error("DevicePage", $"Failed to unregister receiver: {e}");
                    // Still mark as unregistered since the error might be due to already being unregistered
                    isReceiverRegistered = false;
                }
            }
        }
    }

    private void SendUssScan(bool enable)
    {
        try
        {
            Intent intent = new Intent();
            intent.SetAction(systemUssTriggerScan);
            intent.PutExtra(ExtraScan, enable);

            // Send broadcast using Android context
            Android.App.Application.Context.SendBroadcast(intent);

            Log.Debug("DevicePage", $"USS scan broadcast sent: {enable}");
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error sending USS scan broadcast: {ex}");
        }
    }

    private Bundle[] GetParams(Bundle bundle)
    {
        if (bundle == null)
        {
            return null;
        }

        try
        {
            // Get all keys from the bundle
            var keySet = bundle.KeySet();
            if (keySet == null)
            {
                Log.Debug("DevicePage", "Bundle keySet is null");
                return new Bundle[0];
            }

            Bundle[] parameters = new Bundle[keySet.Count];
            int i = 0;

            // Iterate through each key in the bundle
            foreach (string key in keySet)
            {
                Bundle tmp = new Bundle();
                tmp.PutString("Key", key);
                tmp.PutString("Value", bundle.GetString(key));
                parameters[i++] = tmp;
            }

            return parameters;
        }
        catch (System.Exception ex)
        {
            Log.Error("DevicePage", $"Error converting bundle to parameters: {ex}");
            return new Bundle[0];
        }
    }

    /// <summary>
    /// Debug method to show all key-value pairs in a Bundle
    /// </summary>
    /// <param name="bundle">Bundle to display</param>
    /// <param name="bundleName">Name for logging identification</param>
    private void ShowAllBundleValues(Bundle bundle, string bundleName = "Bundle")
    {
        if (bundle == null)
        {
            Log.Debug("DevicePage", $"{bundleName} is null");
            return;
        }

        try
        {
            var keySet = bundle.KeySet();
            if (keySet == null || keySet.Count == 0)
            {
                Log.Debug("DevicePage", $"{bundleName} is empty or has no keys");
                return;
            }

            Log.Debug("DevicePage", $"=== {bundleName} Contents ({keySet.Count} items) ===");

            foreach (string key in keySet)
            {
                try
                {
                    // Try to get the value as different types
                    var obj = bundle.Get(key);
                    if (obj == null)
                    {
                        Log.Debug("DevicePage", $"  {key}: null");
                    }
                    else
                    {
                        string valueStr = "";
                        string typeStr = obj.GetType().Name;

                        // Handle different data types
                        if (obj is Java.Lang.String javaStr)
                        {
                            valueStr = javaStr.ToString();
                        }
                        else if (obj is Java.Lang.Integer javaInt)
                        {
                            valueStr = javaInt.ToString();
                        }
                        else if (obj is Java.Lang.Boolean javaBool)
                        {
                            valueStr = javaBool.ToString();
                        }
                        else if (obj is Java.Lang.Double javaDouble)
                        {
                            valueStr = javaDouble.ToString();
                        }
                        else if (obj is Java.Lang.Float javaFloat)
                        {
                            valueStr = javaFloat.ToString();
                        }
                        else if (obj is Bundle nestedBundle)
                        {
                            valueStr = $"Nested Bundle with {nestedBundle.KeySet()?.Count ?? 0} keys";
                        }
                        else
                        {
                            valueStr = obj.ToString();
                        }

                        Log.Debug("DevicePage", $"  {key} ({typeStr}): {valueStr}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("DevicePage", $"  {key}: Error reading value - {ex}");
                }
            }

            Log.Debug("DevicePage", $"=== End of {bundleName} ===");
        }
        catch (Exception ex)
        {
            Log.Error("DevicePage", $"Error showing bundle values for {bundleName}: {ex}");
        }
    }

    private string GetKeyCodeByName(string filter)
    {
        try
        {
            string keymappingPath = GetKeyMappingPath();

            // Check the end of keymappingPath is "/"
            if (!keymappingPath.EndsWith("/"))
            {
                keymappingPath += "/";
            }

            keymappingPath += "default_keycodes.txt";

            Log.Debug("DevicePage", $"Reading key mapping from: {keymappingPath}");

            // Check if file exists and is accessible
            if (!System.IO.File.Exists(keymappingPath))
            {
                Log.Error("DevicePage", $"Key mapping file not found: {keymappingPath}");
                return null;
            }

            var fileCtrl = Com.Unitech.Api.File.FileCtrl.GetInstance(Android.App.Application.Context);
            Bundle result = fileCtrl.ReadFromFile(keymappingPath);


            ShowAllBundleValues(result, "ReadFromFile Result");

            if (result.GetInt("errorCode") != 0)
            {
                Log.Error("DevicePage", $"Error reading key mapping file: {result.GetString("errorMsg")}");
                return null;
            }

            byte[] fileData = result.GetByteArray("Data");

            // Convert byte array to string and parse XML directly
            string xmlContent = System.Text.Encoding.UTF8.GetString(fileData);

            // Parse XML content using XDocument
            var xmlDoc = System.Xml.Linq.XDocument.Parse(xmlContent);

            // Find the Key element with matching KeyName
            var keyElement = xmlDoc.Descendants("Key")
                .FirstOrDefault(key =>
                {
                    var keyNameElement = key.Element("KeyName");
                    return keyNameElement != null && keyNameElement.Value.Trim().Equals(filter, StringComparison.OrdinalIgnoreCase);
                });

            if (keyElement != null)
            {
                var keyCodeElement = keyElement.Element("KeyCode");
                if (keyCodeElement != null)
                {
                    string keyCode = keyCodeElement.Value.Trim();

                    // Extract only digits if the value contains non-numeric characters
                    if (!string.IsNullOrEmpty(keyCode))
                    {
                        var digitMatch = System.Text.RegularExpressions.Regex.Match(keyCode, @"\d+");
                        if (digitMatch.Success)
                        {
                            Log.Debug("DevicePage", $"Found key code for '{filter}': {digitMatch.Value}");
                            return digitMatch.Value;
                        }
                    }
                }
            }

        }
        catch (Exception ex)
        {
            Log.Error("DevicePage", $"Error reading key code for '{filter}': {ex}");
            return null;
        }

        Log.Debug("DevicePage", $"Key code not found for filter: {filter}");
        return null;
    }

    string GetKeyMappingPath()
    {
        string defaultKeyConfigPath = keymappingPath;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            defaultKeyConfigPath = android12keymappingPath;
        }

        Log.Debug("DevicePage", $"Key mapping path: {defaultKeyConfigPath}");

        return defaultKeyConfigPath;
    }

    void SetUseGunKeyCode()
    {
        if (tempKeyCode == null)
        {
            Task.Run(() =>
            {
                string keyName = "";
                string keyCode = "";

                SendUssScan(false);

                Log.Debug("DevicePage", "Export keyMappings - Start");
                Bundle exportBundle = KeymappingCtrl.GetInstance(Android.App.Application.Context).ExportKeyMappings(GetKeyMappingPath());
                ShowAllBundleValues(exportBundle, "ExportKeyMappings");

                Log.Debug("DevicePage", "Enable KeyMapping - Start");
                Bundle enableBundle = KeymappingCtrl.GetInstance(Android.App.Application.Context).EnableKeyMapping(true);
                ShowAllBundleValues(enableBundle, "EnableKeyMapping");

                switch (Build.Device)
                {
                    case "HT730":
                        keyName = "TRIGGER_GUN";
                        keyCode = "298";

                        tempKeyCode = KeymappingCtrl.GetInstance(Android.App.Application.Context).GetKeyMapping(keyName);

                        // Show all values in tempKeyCode for debugging
                        ShowAllBundleValues(tempKeyCode, "TempKeyCode_HT730");
                        break;
                    case "PA768":
                        {
                            keyName = "SCAN_GUN";
                            // keyCode = "294";

                            tempKeyCode = KeymappingCtrl.GetInstance(Android.App.Application.Context).GetKeyMapping(keyName);

                            // Show all values in tempKeyCode for debugging
                            ShowAllBundleValues(tempKeyCode, "TempKeyCode_PA768");

                            keyCode = tempKeyCode.GetString("KeyCode");

                            // Bundle extendKeyBundle = KeymappingCtrl.getInstance(MainActivity.getInstance().getApplicationContext()).getKeyMapping("SCAN_EXTENDED");
                            // string extendKeyCode = KeymappingCtrl.GetInstance(Android.App.Application.Context)("SCAN_EXTENDED");
                            string extendKeyCode = GetKeyCodeByName("SCAN_EXTENDED");
                            Log.Debug("DevicePage", "SCAN_EXTENDED:" + extendKeyCode);
                            keyCode = extendKeyCode;
                            break;
                        }
                    default:
                        Log.Debug("DevicePage", "Skip to set gun key code");
                        return;
                }

                Log.Debug("DevicePage", "Set Gun Key Code: " + keyCode);
                bool wakeup = tempKeyCode.GetBoolean("wakeUp");
                Bundle[] broadcastDownParams = GetParams(tempKeyCode.GetBundle("broadcastDownParams"));
                Bundle[] broadcastUpParams = GetParams(tempKeyCode.GetBundle("broadcastUpParams"));
                Bundle[] startActivityParams = GetParams(tempKeyCode.GetBundle("startActivityParams"));

                Bundle resultBundle = KeymappingCtrl.GetInstance(Android.App.Application.Context).AddKeyMappings(
                        keyName,
                        keyCode,
                        wakeup,
                        rfidGunPressed,
                        broadcastDownParams,
                        rfidGunReleased,
                        broadcastUpParams,
                        startActivityParams
                );
                if (resultBundle.GetInt("errorCode") == 0)
                {
                    Log.Debug("DevicePage", "Set Gun Key Code success");
                }
                else
                {
                    Log.Error("DevicePage", "Set Gun Key Code failed: " + resultBundle.GetString("errorMsg"));
                }

            });

        }
        else
        {
            Log.Debug("DevicePage", "tempKeyCode is not null, don't set Gun Key Code.");
        }
    }

    void RestoreGunKeyCode()
    {
        if (tempKeyCode != null)
        {
            Task.Run(() =>
            {
                try
                {
                    Log.Debug("DevicePage", "Restoring gun key start");
                    string keyMappingPath = GetKeyMappingPath();
                    Bundle resultBundle = KeymappingCtrl.GetInstance(Android.App.Application.Context).ImportKeyMappings(keyMappingPath);
                    Log.Debug("DevicePage", "Gun key restoration completed");

                    if (resultBundle.GetInt("errorCode") == 0)
                    {
                        Log.Debug("DevicePage", "Gun key restored successfully");
                    }
                    else
                    {
                        Log.Error("DevicePage", "Gun key restoration failed: " + resultBundle.GetString("errorMsg"));
                    }

                    tempKeyCode = null;
                }
                catch (Exception ex)
                {
                    Log.Error("DevicePage", $"Error restoring gun key: {ex}");
                }
            });
        }
    }

    void SetDataCollectionMode()
    {
        try
        {
            baseReader.DataCollectionMode = DataCollectionMode.Auto;
        }
        catch (Exception ex)
        {
            Log.Warn("DevicePage", $"Error setting data collection mode: {ex}");
        }
    }

    public void OnExtendedPortEvent(Intent intent)
    {
        int extra = intent.GetIntExtra("EXTRA_EXTENDED_STATE", -1);
        Log.Debug("DevicePage", "Gun detect: " + extra);
        Log.Debug("DevicePage", "deviceType: " + deviceType.ToString());
        if (deviceType == Com.Unitech.Lib.Types.DeviceType.Rg768)
        {
            if (extra == 1)
            {
                // Use C# Task.Delay instead of Java Handler.postDelayed
                Task.Delay(2500).ContinueWith((task) =>
                {
                    Log.Debug("DevicePage", "gunAttached detect: Home device is RG768");
                    try
                    {
                        baseReader = new RG768Reader(Android.App.Application.Context);
                        baseReader.AddListener(readerEventHandler);
                        baseReader.SetRfidEventListener(rfidEventHandler);
                        baseReader.Connect();
                    }
                    catch (System.Exception e)
                    {
                        Log.Error("DevicePage", "Connect exception: " + e.ToString());
                    }
                });
            }
            else
            {
                try
                {
                    baseReader?.Disconnect();
                }
                catch (System.Exception e)
                {
                    Log.Error("DevicePage", "Disconnect exception: " + e.ToString());
                }
            }
        }
    }
}

// BroadcastReceiver for handling RFID gun key events
public class RfidBroadcastReceiver : BroadcastReceiver
{
    private readonly DevicePage devicePage;

    public RfidBroadcastReceiver(DevicePage devicePage)
    {
        this.devicePage = devicePage;
    }

    public override void OnReceive(Context context, Intent intent)
    {
        string action = intent.Action;

        try
        {
            Log.Debug("RfidBroadcastReceiver", $"Received action: {action}");

            switch (action)
            {
                case "com.unitech.RFID_GUN.PRESSED":
                    Log.Debug("RfidBroadcastReceiver", "RFID gun pressed");
                    // Handle gun pressed event
                    devicePage?.ReceiveKeyChange(KeyType.Trigger, KeyState.KeyDown);
                    break;

                case "com.unitech.RFID_GUN.RELEASED":
                    Log.Debug("RfidBroadcastReceiver", "RFID gun released");
                    // Handle gun released event
                    devicePage?.ReceiveKeyChange(KeyType.Trigger, KeyState.KeyUp);
                    break;

                case "com.unitech.EXTENDED_PORT":
                    devicePage?.OnExtendedPortEvent(intent);
                    break;

                default:
                    Log.Debug("RfidBroadcastReceiver", $"Unhandled action: {action}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("RfidBroadcastReceiver", $"Error handling broadcast: {ex}");
        }
    }
}

