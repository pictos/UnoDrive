﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using CommunityToolkit.Mvvm.ComponentModel;
using UnoDrive.Data;
using UnoDrive.Mvvm;
using UnoDrive.Services;
using Windows.Networking.Connectivity;

namespace UnoDrive.ViewModels
{
	public class DashboardViewModel : ObservableObject, IAuthenticationProvider, IInitialize
    {
		IDataStore dataStore;
		INetworkConnectivityService networkService;
		ILogger logger;
		public DashboardViewModel(
			IDataStore dataStore,
			INetworkConnectivityService networkService,
			ILogger<DashboardViewModel> logger)
		{
			this.dataStore = dataStore;
			this.networkService = networkService;
			this.logger = logger;
		}

		string name = "Placeholder";
		public string Name
		{
			get => name;
			set => SetProperty(ref name, value);
		}

		string email = "Placeholder";
		public string Email
		{
			get => email;
			set => SetProperty(ref email, value);
		}

		public async Task LoadDataAsync()
		{
			try
			{
				var objectId = ((App)App.Current).AuthenticationResult.Account.HomeAccountId.ObjectId;
				var userInfo = dataStore.GetUserInfoById(objectId);
				if (userInfo != null)
				{
					Name = userInfo.Name;
					Email = userInfo.Email;
				}

				if (networkService.Connectivity != NetworkConnectivityLevel.InternetAccess)
				{
					return;
				}
#if __WASM__
				var httpClient = new HttpClient(new Uno.UI.Wasm.WasmHttpHandler());
#else
				var httpClient = new HttpClient();
#endif

				var graphClient = new GraphServiceClient(httpClient);
				graphClient.AuthenticationProvider = this;

				var request = graphClient.Me
					.Request()
					.Select(user => new
					{
						DisplayName = user.DisplayName,
						UserPrincipalName = user.UserPrincipalName
					});

#if __ANDROID__ || __IOS__ || __MACOS__
				var response = await request.GetResponseAsync();
				var data = await response.Content.ReadAsStringAsync();
				var me = JsonSerializer.Deserialize<User>(data);
#else
				var me = await request.GetAsync();
#endif

				if (me != null)
				{
					Name = me.DisplayName;
					Email = me.UserPrincipalName;

					userInfo = new UserInfo
					{
						Id = objectId,
						Name = Name,
						Email = Email
					};
					dataStore.SaveUserInfo(userInfo);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, ex.Message);
			}
		}

		public Task AuthenticateRequestAsync(HttpRequestMessage request)
		{
			var token = ((App)App.Current).AuthenticationResult?.AccessToken;
			if (string.IsNullOrEmpty(token))
				throw new Exception("No Access Token");

			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			return Task.CompletedTask;
		}

		public async Task InitializeAsync()
		{
			await LoadDataAsync();
		}
	}
}
