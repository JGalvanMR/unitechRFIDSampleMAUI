using System;
using System.Threading.Tasks;
using Android;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Xamarin.Google.Crypto.Tink.Signature;

namespace unitechRFIDSampleMAUI.Helpers
{
    public static class PermissionHelper
    {
        public static async Task<bool> CheckAndRequestPermissionsAsync()
        {
            try
            {
                var permissionsToCheck = new List<(string Name, Func<Task<PermissionStatus>> CheckFunc, Func<Task<PermissionStatus>> RequestFunc)>
                {
                    ("Location",
                    () => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>(),
                    () => Permissions.RequestAsync<Permissions.LocationWhenInUse>())
                };

                // First, check all permissions without requesting
                var deniedPermissions = new List<string>();

                foreach (var (name, checkFunc, _) in permissionsToCheck)
                {
                    try
                    {
                        var status = await checkFunc();
                        if (status != PermissionStatus.Granted)
                        {
                            deniedPermissions.Add(name);
                            System.Diagnostics.Debug.WriteLine($"{name} permission not granted: {status}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"{name} permission already granted");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking {name} permission: {ex.Message}");
                        deniedPermissions.Add(name);
                    }
                }

                // Check Android-specific permissions
                var androidDeniedPermissions = CheckAndroidSpecificPermissionsStatus();

                if (androidDeniedPermissions.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Android permissions denied: {string.Join(", ", androidDeniedPermissions)}");
                }

                // If no permissions are denied, we're done
                if (deniedPermissions.Count == 0 && androidDeniedPermissions.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("All permissions granted successfully!");
                    return true;
                }

                // Show what we're requesting
                var allDeniedPermissions = new List<string>();
                allDeniedPermissions.AddRange(deniedPermissions);
                allDeniedPermissions.AddRange(androidDeniedPermissions);
                System.Diagnostics.Debug.WriteLine($"Requesting permissions: {string.Join(", ", allDeniedPermissions)}");

                // Request MAUI Essentials permissions
                foreach (var (name, _, requestFunc) in permissionsToCheck)
                {
                    if (deniedPermissions.Contains(name))
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Requesting {name} permission...");
                            var status = await requestFunc();
                            System.Diagnostics.Debug.WriteLine($"{name} permission result: {status}");

                            if (status == PermissionStatus.Denied)
                            {
                                System.Diagnostics.Debug.WriteLine($"{name} permission explicitly denied by user");
                                // Don't return false immediately, let the retry logic handle it
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error requesting {name} permission: {ex.Message}");
                        }
                    }
                }

                // Request Android-specific permissions
                if (androidDeniedPermissions.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Requesting Android-specific permissions...");
                    await RequestAndroidSpecificPermissions(androidDeniedPermissions);
                }

                // Wait a moment for permissions to be processed
                await Task.Delay(1000);

                // Check again after requesting - if all are granted now, we're done
                bool allNowGranted = true;

                // Check MAUI permissions again
                foreach (var (name, checkFunc, _) in permissionsToCheck)
                {
                    try
                    {
                        var status = await checkFunc();
                        if (status != PermissionStatus.Granted)
                        {
                            System.Diagnostics.Debug.WriteLine($"{name} still not granted after request: {status}");
                            allNowGranted = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error re-checking {name} permission: {ex.Message}");
                        allNowGranted = false;
                    }
                }

                // Check Android permissions again
                var stillDeniedAndroidPermissions = CheckAndroidSpecificPermissionsStatus();
                if (stillDeniedAndroidPermissions.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Android permissions still denied: {string.Join(", ", stillDeniedAndroidPermissions)}");
                    allNowGranted = false;
                }

                if (allNowGranted)
                {
                    System.Diagnostics.Debug.WriteLine("All permissions granted after request!");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking permissions: {ex.Message}");
                return false;
            }
        }

        private static List<string> CheckAndroidSpecificPermissionsStatus()
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            var deniedPermissions = new List<string>();

            // List of Android-specific permissions that might be needed for RFID
            var androidPermissions = new[]
            {
                Manifest.Permission.AccessCoarseLocation,
                Manifest.Permission.AccessFineLocation,
            };

            // Check android version to add permission
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                androidPermissions = androidPermissions.Append(Manifest.Permission.BluetoothScan).ToArray();
                androidPermissions = androidPermissions.Append(Manifest.Permission.BluetoothConnect).ToArray();
                androidPermissions = androidPermissions.Append(Manifest.Permission.BluetoothAdvertise).ToArray();
            }

            foreach (var permission in androidPermissions)
            {
                try
                {
                    if (ContextCompat.CheckSelfPermission(context, permission) != Permission.Granted)
                    {
                        deniedPermissions.Add(permission);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking {permission}: {ex.Message}");
                    deniedPermissions.Add(permission);
                }
            }

            return deniedPermissions;
        }

        private static async Task<bool> RequestAndroidSpecificPermissions(List<string> permissionsToRequest)
        {
            if (permissionsToRequest.Count == 0)
                return true;

            var context = Platform.CurrentActivity ?? Android.App.Application.Context;

            if (Platform.CurrentActivity is AndroidX.AppCompat.App.AppCompatActivity activity)
            {
                System.Diagnostics.Debug.WriteLine($"Requesting {permissionsToRequest.Count} Android permissions");

                // Show rationale if needed
                bool shouldShowRationale = false;
                foreach (var permission in permissionsToRequest)
                {
                    if (ActivityCompat.ShouldShowRequestPermissionRationale(activity, permission))
                    {
                        shouldShowRationale = true;
                        break;
                    }
                }

                // if (shouldShowRationale)
                // {
                //     System.Diagnostics.Debug.WriteLine("Showing permission rationale dialog");
                //     bool result = await Application.Current.MainPage.DisplayAlert(
                //         "Permissions Required",
                //         "This app needs location and Bluetooth permissions to communicate with RFID devices.",
                //         "Grant Permissions",
                //         "Cancel");

                //     if (!result)
                //     {
                //         System.Diagnostics.Debug.WriteLine("User cancelled permission rationale");
                //         return false;
                //     }
                // }

                // Request permissions
                try
                {
                    var requestCode = 1001;
                    System.Diagnostics.Debug.WriteLine($"Requesting permissions: {string.Join(", ", permissionsToRequest)}");
                    ActivityCompat.RequestPermissions(activity, permissionsToRequest.ToArray(), requestCode);

                    // Wait longer for the permission dialogs to complete
                    await Task.Delay(3000);

                    System.Diagnostics.Debug.WriteLine("Checking permission results after request");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error requesting Android permissions: {ex.Message}");
                    return false;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No activity available for permission request");
                return false;
            }

            return true; // Don't check results here, let the main method handle verification
        }

        public static async Task<bool> CheckLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return status == PermissionStatus.Granted;
        }

        public static async Task<bool> CheckStoragePermissionAsync()
        {
            var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

            if (readStatus != PermissionStatus.Granted)
            {
                readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            if (writeStatus != PermissionStatus.Granted)
            {
                writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }

            return readStatus == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
        }
    }
}
