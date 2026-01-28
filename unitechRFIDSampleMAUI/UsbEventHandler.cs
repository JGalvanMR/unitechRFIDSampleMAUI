
using Android.Hardware.Usb;
using Com.Unitech.Lib.Transport.Event;

namespace unitechRFIDSampleMAUI
{
    public class UsbEventHandler : Java.Lang.Object, IUsbEventListener
    {
        private readonly DevicePage devicePage;

        public UsbEventHandler(DevicePage page)
        {
            try
            {
                devicePage = page;
                Android.Util.Log.Debug("UsbEventHandler", "UsbEventHandler initialized successfully");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("UsbEventHandler", "Error initializing UsbEventHandler: " + ex.ToString());
                throw;
            }
        }

        public void OnUsbAttached(UsbDevice device)
        {
            try
            {
                devicePage.OnUsbAttached(device);
                Android.Util.Log.Debug("UsbEventHandler", $"OnUsbAttached: {device.DeviceName}");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("UsbEventHandler", "Error in OnUsbAttached: " + ex.ToString());
            }
        }

        public void OnUsbDetached(UsbDevice device)
        {
            try
            {
                devicePage.OnUsbDetached(device);
                Android.Util.Log.Debug("UsbEventHandler", $"OnUsbDetached: {device.DeviceName}");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("UsbEventHandler", "Error in OnUsbDetached: " + ex.ToString());
            }
        }
    }
}
