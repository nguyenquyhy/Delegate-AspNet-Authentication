using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AspNetAuthentication.WpfClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string Authority = "https://login.microsoftonline.com/common/v2.0";
        
        //============= TO FILL IN ===============//
        private const string ClientId = "";
        private static readonly string[] Scopes = { "[api://...API ID.../...scope name...]" };
        private const string BaseUrl = "https://localhost:44364";
        //========================================//

        // computing the root directory is not very simple on Linux and Mac, so a helper is provided
        private static readonly string s_cacheFilePath =
                   Path.Combine(MsalCacheHelper.UserRootDirectory, "msal.contoso.cache");

        public static readonly string CacheFileName = Path.GetFileName(s_cacheFilePath);
        public static readonly string CacheDir = Path.GetDirectoryName(s_cacheFilePath);

        private readonly IPublicClientApplication app;
        private readonly HttpClient httpClient;

        public MainWindow()
        {
            InitializeComponent();

            app = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(Authority)
                .WithDefaultRedirectUri()
                .Build();

            httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        }

        private async void ButtonSignIn_Click(object sender, RoutedEventArgs e)
        {
            await InitializeCacheAsync();
            await SignInAsync();
        }

        private async void ButtonSignOut_Click(object sender, RoutedEventArgs e)
        {
            await SignOutAsync();
        }

        private async Task InitializeCacheAsync()
        {
            // Building StorageCreationProperties
            var storageProperties =
                 new StorageCreationPropertiesBuilder(
                     CacheFileName,
                     CacheDir,
                     ClientId)
                 .Build();

            // This hooks up the cross-platform cache into MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);
        }

        private async Task SignInAsync()
        {
            var accounts = (await app.GetAccountsAsync()).ToList();

            // Get an access token to call the To Do list service.
            try
            {
                var result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                await Dispatcher.Invoke(async () =>
                {
                    ButtonSignIn.Visibility = Visibility.Collapsed;
                    ButtonSignOut.Visibility = Visibility.Visible;
                    await CallRestAsync(result.AccessToken);
                    await CallGraphQlAsync(result.AccessToken);
                });
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    // Force a sign-in (Prompt.SelectAccount), as the MSAL web browser might contain cookies for the current user
                    // and we don't necessarily want to re-sign-in the same user
                    var result = await app.AcquireTokenInteractive(Scopes)
                        .WithAccount(accounts.FirstOrDefault())
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync()
                        .ConfigureAwait(false);

                    await Dispatcher.Invoke(async () =>
                    {
                        ButtonSignIn.Visibility = Visibility.Collapsed;
                        ButtonSignOut.Visibility = Visibility.Visible;
                        await CallRestAsync(result.AccessToken);
                        await CallGraphQlAsync(result.AccessToken);
                    });
                }
                catch (MsalException ex)
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

                    Dispatcher.Invoke(() =>
                    {
                        //UserName.Content = Properties.Resources.UserNotSignedIn;
                    });
                }
            }
        }

        private async Task SignOutAsync()
        {
            var accounts = (await app.GetAccountsAsync()).ToList();

            await app.RemoveAsync(accounts.FirstOrDefault());

            ButtonSignIn.Visibility = Visibility.Visible;
            ButtonSignOut.Visibility = Visibility.Collapsed;
        }

        private async Task CallRestAsync(string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.GetAsync(new Uri(new Uri(BaseUrl, UriKind.Absolute), "/api/Values/Profile"));

            if (response.IsSuccessStatusCode)
            {
                var dataString = await response.Content.ReadAsStringAsync();
                MessageBox.Show("Got from REST API: " + dataString);
            }
            else
            {
                MessageBox.Show("Failed to get REST data! " + response.StatusCode);
            }
        }

        private async Task CallGraphQlAsync(string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.PostAsync(new Uri(new Uri(BaseUrl, UriKind.Absolute), "/GraphQL"),
                new StringContent("{ \"query\": \"{ profile }\" }", Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var dataString = await response.Content.ReadAsStringAsync();
                MessageBox.Show("Got from GraphQL: " + dataString);
            }
            else
            {
                MessageBox.Show("Failed to get GraphQL data!" + response.StatusCode);
            }
        }
    }
}
