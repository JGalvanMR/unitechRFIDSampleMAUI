
using Com.Unitech.Lib.Event;
using Com.Unitech.Lib.Types;
using Com.Unitech.Lib.Uhf;
using Com.Unitech.Lib.Uhf.Params;

namespace unitechRFIDSampleMAUI
{
    public class RfidEventHandler : Java.Lang.Object, IRfidEventListener
    {
        private readonly DevicePage devicePage;

        public RfidEventHandler(DevicePage page)
        {
            try
            {
                devicePage = page;
                Android.Util.Log.Debug("RfidEventHandler", "RfidEventHandler initialized successfully");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("RfidEventHandler", "Error initializing RfidEventHandler: " + ex.ToString());
                throw;
            }
        }

        public void OnAccessResult(BaseUHF uhf, ResultCode code, ActionState action, string epc, string data, Java.Lang.Object @params)
        {
            try
            {
                devicePage.ReceiveTagAccessResult(epc, data, action, code);
                Android.Util.Log.Debug("RfidEventHandler", $"OnAccessResult: {action}, Code: {code}, EPC: {epc}, Data: {data}");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("RfidEventHandler", "Error in OnAccessResult: " + ex.ToString());
            }
        }

        public void OnReadTag(BaseUHF uhf, string tag, Java.Lang.Object @params)
        {
            try
            {
                TagExtParam tagExtParam = @params as TagExtParam;
                devicePage.ReceiveTag(tag, tagExtParam);


                Android.Util.Log.Debug("RfidEventHandler", $"OnReadTag: {tagExtParam}");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("RfidEventHandler", "Error in OnReadTag: " + ex.ToString());
            }
        }
    }
}