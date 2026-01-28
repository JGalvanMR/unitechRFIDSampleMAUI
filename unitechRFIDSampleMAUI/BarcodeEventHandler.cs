
using Com.Unitech.Lib.Event;
using Com.Unitech.Lib.Event.Params;

namespace unitechRFIDSampleMAUI
{
    public class BarcodeEventHandler : Java.Lang.Object, IBarcodeEventListener
    {
        private readonly DevicePage devicePage;

        public BarcodeEventHandler(DevicePage page)
        {
            try
            {
                devicePage = page;
                Android.Util.Log.Debug("BarcodeEventHandler", "BarcodeEventHandler initialized successfully");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("BarcodeEventHandler", "Error initializing BarcodeEventHandler: " + ex.ToString());
                throw;
            }
        }

        public void OnBarcodeEvent(BarcodeEventArgs barcodeEventArgs)
        {
            devicePage.ReceiveBarcode(barcodeEventArgs);
        }
    }
}