
using Android.Util;
using Com.Unitech.Lib.Reader;
using Com.Unitech.Lib.Reader.Event;
using Com.Unitech.Lib.Reader.Types;
using Com.Unitech.Lib.Transport.Types;
using Com.Unitech.Lib.Types;
using Com.Unitech.Lib.Params;

namespace unitechRFIDSampleMAUI
{
    public class ReaderEventHandler : Java.Lang.Object, IReaderEventListener
    {
        private readonly DevicePage devicePage;

        public ReaderEventHandler(DevicePage page)
        {
            try
            {
                devicePage = page;
                Log.Debug("ReaderEventListener", "ReaderEventListener initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error("ReaderEventListener", "Error initializing ReaderEventListener: " + ex.ToString());
                throw;
            }
        }

        public void OnAntennaStatus(BaseReader reader, Com.Unitech.Lib.Params.AntennaStatusEventArgs eventArgs, Java.Lang.Object @params)
        {
            // Log.Debug("ReaderEventListener", "Antenna status changed: " + eventArgs.Status);
        }

        public void OnBatchDataEvent(BaseReader reader, BatchDataStatus status, Java.Lang.Object @params)
        {
            // Log.Debug("ReaderEventListener", "Batch data event: " + status);
        }

        public void OnFirmwareUpdateProgress(BaseReader reader, FirmwareUpdateEventArgs firmwareUpdateEventArgs, Java.Lang.Object @params)
        {
            // Log.Debug("ReaderEventListener", "Firmware update progress: " + firmwareUpdateEventArgs.State + firmwareUpdateEventArgs.Percentage + "%");
            throw new NotImplementedException();
        }

        public void OnLBTStatus(BaseReader reader, Com.Unitech.Lib.Params.LBTStatusEventArgs lbtStatusEventArgs, Java.Lang.Object @params)
        {
            // Log.Debug("ReaderEventListener", "LBT status changed: " + lbtStatusEventArgs.ToString());
        }

        public void OnNotificationState(NotificationState state, Java.Lang.Object @params)
        {
            // Log.Debug("ReaderEventListener", "Notification state changed: " + state);
        }

        public void OnReaderActionChanged(BaseReader reader, ResultCode retCode, ActionState state, Java.Lang.Object @params)
        {
            try
            {
                Log.Debug("ReaderEventListener", $"Reader action changed: {state}, Result: {retCode}");

                if (devicePage != null)
                {
                    // Handle the action state change on the main thread
                    devicePage.UpdateActionState(state);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ReaderEventListener", "Error in OnReaderActionChanged: " + ex.ToString());
            }
        }

        public void OnReaderBatteryState(BaseReader reader, int batteryState, Java.Lang.Object @params)
        {
            try
            {
                Log.Debug("ReaderEventListener", "Reader battery state changed: " + batteryState);

                // Update the battery level in the DevicePage
                if (devicePage != null)
                {
                    devicePage.UpdateBatteryState(batteryState);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ReaderEventListener", "Error in OnReaderBatteryState: " + ex.ToString());
            }
        }

        public void OnReaderKeyChanged(BaseReader reader, KeyType type, KeyState state, Java.Lang.Object @params)
        {
            try
            {
                // Update the key state in the DevicePage
                if (devicePage != null)
                {
                    Log.Info("ReaderEventListener", $"Reader key changed - Type: {type}, State: {state}");
                    devicePage.UpdateKeyState(type, state);
                }
                else
                {
                    Log.Warn("ReaderEventListener", "DevicePage is null");
                }
            }
            catch (Exception ex)
            {
                Log.Error("ReaderEventListener", "Error in OnReaderKeyChanged: " + ex.ToString());
            }
        }

        public void OnReaderStateChanged(BaseReader reader, ConnectState state, Java.Lang.Object @params)
        {
            try
            {
                Log.Debug("ReaderEventListener", $"Reader state changed: {state}");

                // Handle connection state changes on the main thread
                if (devicePage != null)
                {
                    devicePage.UpdateConnectionState(state);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ReaderEventListener", "Error in OnReaderStateChanged: " + ex.ToString());
            }
        }

        public void OnReaderTemperatureState(BaseReader reader, double temperatureState, Java.Lang.Object @params)
        {
            try
            {
                Log.Debug("ReaderEventListener", "Reader temperature state changed: " + temperatureState);

                // Update the temperature in the DevicePage
                if (devicePage != null)
                {
                    devicePage.UpdateTemperatureState(temperatureState);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ReaderEventListener", "Error in OnReaderTemperatureState: " + ex.ToString());
            }
        }
    }
}