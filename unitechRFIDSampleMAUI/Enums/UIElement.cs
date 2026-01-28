namespace unitechRFIDSampleMAUI.Enums;

/// <summary>
/// Enum representing UI elements that can be updated
/// </summary>
public enum UIElement
{
    // Labels
    ConnectStateLabel,
    BatteryLabel,
    TemperatureLabel,
    EpcLabel,
    RssiLabel,
    TidLabel,
    SgtinLabel,
    ProgressLabel,
    ProgressPercentLabel,
    ResultLabel,
    DataLabel,
    TimestampLabel,
    BarcodeIdLabel,
    BarcodeDataLabel,

    // Buttons
    InfoButton,
    SettingsButton,
    InventoryButton,
    CustomInventoryButton,
    FindTagButton,
    ReadButton,
    WriteButton,
    LockButton,
    UnlockButton,
    SendButton,

    // Switches
    FastIdSwitch,
    Gen2xSwitch,

    // Containers/Rows
    TidRow,
    BarcodeIdRow,
    BarcodeDataRow,
    FastIdRow,
    Gen2xRow,
    DisplayTextContainer,

    // Progress
    ProgressBar
}

/// <summary>
/// Enum representing UI update types
/// </summary>
public enum UIUpdateType
{
    TextOnly,
    TextAndColor,
    ButtonState,
    ButtonEnabled,
    ButtonDisabled
}
