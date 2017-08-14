//----------------------------------------------------------------------------------------------
//    Copyright 2014 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//----------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// The following using statements were added for this sample.
using System.Globalization;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Configuration;
using System.Net;
using System.Collections.Specialized;
using RestSharp;
using Newtonsoft.Json;

namespace TodoListClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Redirect URI is the URI where Azure AD will return OAuth responses.
        // The Authority is the sign-in URL of the tenant.
        //
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        Uri redirectUri = new Uri(ConfigurationManager.AppSettings["ida:RedirectUri"]);

        private static string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        private static string todoListResourceId = ConfigurationManager.AppSettings["todo:TodoListResourceId"];
        private static string todoListBaseAddress = ConfigurationManager.AppSettings["todo:TodoListBaseAddress"];

        private HttpClient httpClient = new HttpClient();
        private AuthenticationContext authContext = null;

		private AuthenticationResult authResult = null;

		public  MainWindow()
        {
            InitializeComponent();
            authContext = new AuthenticationContext(authority, new FileCache());
            GetTodoList(true);
        }

        private void GetTodoList()
        {
            GetTodoList(false);
        }

        private async void GetTodoList(bool isAppStarting)
        {
			//
			// Get an access token to call the To Do service.
			//


			// Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do list service.
			if (authResult == null)
			{
				authResult = await AdalDialog2Async(isAppStarting);
			}

			if (isAppStarting)
			{
				return;
			}

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/todolist");

            if (response.IsSuccessStatusCode)
            {

                // Read the response and databind to the GridView to display To Do items.
                string s = await response.Content.ReadAsStringAsync();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<TodoItem> toDoArray = serializer.Deserialize<List<TodoItem>>(s);

                TodoList.ItemsSource = toDoArray.Select(t => new { t.Title });
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }

            return;
        }

        private async void AddTodoItem(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TodoText.Text))
            {
                MessageBox.Show("Please enter a value for the To Do item name");
                return;
            }

			//
			// Get an access token to call the To Do service.
			//


			//
			// Call the To Do service.
			//

			// Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do service.
			//httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

			// direct login+password auth
			if (authResult == null)
			{
				authResult = await AdalDialogAsync();
			}
			
			// RestSharp
			RestClient client = new RestClient(todoListBaseAddress);
			RestRequest request = new RestRequest("/api/todolist", Method.POST);
			request.AddParameter(
				"Authorization", 
				string.Format("Bearer " + authResult.AccessToken),
				ParameterType.HttpHeader);

			var json = JsonConvert.SerializeObject(new TodoItem() { Title = TodoText.Text } );
			request.AddParameter("application/json; charset=utf-8", json, ParameterType.RequestBody);
			var restResponse = client.Execute(request);

			//---------------------------------------------------------------
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

			// Forms encode Todo item, to POST to the todo list web api.
			HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", TodoText.Text) });

			// Call the To Do list service.
			HttpResponseMessage response = await httpClient.PostAsync(todoListBaseAddress + "/api/todolist", content);

            if (response.IsSuccessStatusCode)
            {
                TodoText.Text = "";
                GetTodoList();
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }
        }

        private async void SignIn(object sender = null, RoutedEventArgs args = null)
        {
            // If there is already a token in the cache, clear the cache and update the label on the button.
            if (SignInButton.Content.ToString() == "Clear Cache")
            {
                TodoList.ItemsSource = string.Empty;
                authContext.TokenCache.Clear();
                // Also clear cookies from the browser control.
                ClearCookies();
                SignInButton.Content = "Sign In";
                return;
            }

            //
            // Get an access token to call the To Do list service.
            //
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenAsync(todoListResourceId, clientId, redirectUri, new PlatformParameters(PromptBehavior.Always));
                SignInButton.Content = "Clear Cache";
                GetTodoList();
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == "access_denied")
                {
                    // The user canceled sign in, take no action.
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }

                return;
            }

        }

        // This function clears cookies from the browser control used by ADAL.
        private void ClearCookies()
        {
            const int INTERNET_OPTION_END_BROWSER_SESSION = 42;
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_END_BROWSER_SESSION, IntPtr.Zero, 0);
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

		private async void SignIn2Button_Click(object sender, RoutedEventArgs e)
		{

			authResult = await AdalAuthenticationAsync();
			//await HttpAuthenticationAsync();
		}

		private static async Task HttpAuthenticationAsync()
		{
			//  Constants
			var tenant = MainWindow.tenant;
			var serviceUri = MainWindow.todoListResourceId;
			var clientID = MainWindow.clientId;
			var userName = "testuser@valeriilyincbsinteractive.onmicrosoft.com";
			var password = "Test01xx";

			using (var webClient = new WebClient())
			{
				var requestParameters = new NameValueCollection();

				requestParameters.Add("resource", serviceUri);
				requestParameters.Add("client_id", clientID);
				requestParameters.Add("grant_type", "password");
				requestParameters.Add("username", userName);
				requestParameters.Add("password", password);
				requestParameters.Add("scope", "openid");

				var url = $"https://login.microsoftonline.com/{tenant}/oauth2/token";
				var responsebytes = await webClient.UploadValuesTaskAsync(url, "POST", requestParameters);
				var responsebody = Encoding.UTF8.GetString(responsebytes);
			}
		}

		private static async Task<AuthenticationResult> AdalAuthenticationAsync()
		{
			//  Constants
			var tenant = MainWindow.tenant;
			var serviceUri = MainWindow.todoListResourceId;
			var clientID = MainWindow.clientId;
			var userName = "testuser@valeriilyincbsinteractive.onmicrosoft.com";
			var password = "Test01xx";

			//  Ceremony
			var authority = "https://login.microsoftonline.com/" + tenant;
			var authContext = new AuthenticationContext(authority);
			var credentials = new UserPasswordCredential(userName, password);
			var authResult = await authContext.AcquireTokenAsync(serviceUri, clientID, credentials);
			return authResult;
		}

		private async Task<AuthenticationResult> AdalDialogAsync()
		{
			AuthenticationResult result = null;
			try
			{
				result = await authContext.AcquireTokenAsync(todoListResourceId, clientId, redirectUri, new PlatformParameters(PromptBehavior.Never));
			}
			catch (AdalException ex)
			{
				// There is no access token in the cache, so prompt the user to sign-in.
				if (ex.ErrorCode == "user_interaction_required")
				{
					MessageBox.Show("Please sign in first");
					SignInButton.Content = "Sign In";
				}
				else
				{
					// An unexpected error occurred.
					string message = ex.Message;
					if (ex.InnerException != null)
					{
						message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
					}

					MessageBox.Show(message);
				}
			}

			return result;
		}

		private async Task<AuthenticationResult> AdalDialog2Async(bool isAppStarting)
		{
			AuthenticationResult result = null;
			try
			{
				result = await authContext.AcquireTokenAsync(todoListResourceId, clientId, redirectUri, new PlatformParameters(PromptBehavior.Never));
				SignInButton.Content = "Clear Cache";
			}
			catch (AdalException ex)
			{
				// There is no access token in the cache, so prompt the user to sign-in.
				if (ex.ErrorCode == "user_interaction_required")
				{
					if (!isAppStarting)
					{
						MessageBox.Show("Please sign in to view your To-Do list");
						SignInButton.Content = "Sign In";
					}
				}
				else
				{
					// An unexpected error occurred.
					string message = ex.Message;
					if (ex.InnerException != null)
					{
						message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
					}
					MessageBox.Show(message);
				}

			}

			return result;
		}
			

	}
}
