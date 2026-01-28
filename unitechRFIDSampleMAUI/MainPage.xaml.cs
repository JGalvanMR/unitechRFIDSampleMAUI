namespace unitechRFIDSampleMAUI;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		try
		{
			// Check permissions when the main page appears
			System.Diagnostics.Debug.WriteLine("MainPage: Starting permission check process...");
			bool hasPermissions = await CheckAndRequestPermissionsAsync();

			if (!hasPermissions)
			{
				System.Diagnostics.Debug.WriteLine("MainPage: Final permission check failed");
				await DisplayAlert("Permissions Required",
					"Some required permissions were not granted. The app may not function properly. Please check your device settings and grant the necessary permissions.",
					"OK");
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("MainPage: All permissions granted successfully!");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"MainPage: Error during permission check: {ex}");
		}
	}

	private async Task<bool> CheckAndRequestPermissionsAsync()
	{
		try
		{
			return await Helpers.PermissionHelper.CheckAndRequestPermissionsAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"MainPage: Error checking permissions: {ex.Message}");
			await DisplayAlert("Permission Error",
				"Failed to check permissions. Some features may not work correctly.",
				"OK");
			return false;
		}
	}

	private async void OnRP902Clicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new BluetoothConnectionPage(Com.Unitech.Lib.Types.DeviceType.Rp902));
	}

	private async void OnRG768Clicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new DevicePage(Com.Unitech.Lib.Types.DeviceType.Rg768));
	}

	private async void OnHT730Clicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new DevicePage(Com.Unitech.Lib.Types.DeviceType.Ht730));
	}

	private async void OnPA768EClicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new DevicePage(Com.Unitech.Lib.Types.DeviceType.Pa768e));
	}

	private async void OnRP300Clicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new BluetoothConnectionPage(Com.Unitech.Lib.Types.DeviceType.Rp300));
	}

	private async void OnRP300USBClicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new DevicePage(Com.Unitech.Lib.Types.DeviceType.Rp300, true));
	}

	private async void OnEA530Clicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new DevicePage(Com.Unitech.Lib.Types.DeviceType.Ea530));
	}
}
