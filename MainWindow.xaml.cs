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
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using Microsoft.Identity.Client;

namespace WpfApptest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Redirect URI is the URI where Azure AD will return OAuth responses.
        // The Authority is the sign-in URL of the tenant.

        private static readonly string AadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static readonly string Tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static readonly string ClientId = ConfigurationManager.AppSettings["ida:ClientId"];

        private static readonly string Authority = string.Format(CultureInfo.InvariantCulture, AadInstance, Tenant);

        private static readonly string TodoListScope = ConfigurationManager.AppSettings["todo:TodoListScope"];
        private static readonly string[] Scopes = { TodoListScope };

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly IPublicClientApplication _app;
        // Button content
        const string SignInString = "Sign In";
        const string ClearCacheString = "Clear Cache";
        const string UserNotSignedIn = "Not Signed";
        const string UserNotIdentified = "Not Identified";


        public MainWindow()
        {
            InitializeComponent();
            _app = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(Authority)
                .WithDefaultRedirectUri()
                .Build();

            TokenCacheHelper.EnableSerialization(_app.UserTokenCache);
        }


        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            var accounts = (await _app.GetAccountsAsync()).ToList();
            // If there is already a token in the cache, clear the cache and update the label on the button.
            if (SignInButton.Content.ToString() == ClearCacheString)
            {
                // clear the cache
                while (accounts.Any())
                {
                    await _app.RemoveAsync(accounts.First());
                    accounts = (await _app.GetAccountsAsync()).ToList();
                }

                // Also clear cookies from the browser control.
                SignInButton.Content = SignInString;
                UserName.Content = UserNotSignedIn;
                return;
            }

            try
            {
                var result = await _app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                Dispatcher.Invoke(() =>
                    {
                        SignInButton.Content = ClearCacheString;
                        SetUserName(result.Account);
                        
                    }
                );
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    // Force a sign-in (Prompt.SelectAccount), as the MSAL web browser might contain cookies for the current user
                    // and we don't necessarily want to re-sign-in the same user
                    var result = await _app.AcquireTokenInteractive(Scopes)
                        .WithAccount(accounts.FirstOrDefault())
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync()
                        .ConfigureAwait(false);

                    Dispatcher.Invoke(() =>
                        {
                            SignInButton.Content = ClearCacheString;
                            SetUserName(result.Account);
                            
                        }
                    );
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

                    Dispatcher.Invoke(() => { UserName.Content = UserNotSignedIn; });
                }
            }
        }

        // Set user name to text box
        private void SetUserName(IAccount userInfo)
        {
            string userName = null;

            if (userInfo != null)
            {
                userName = userInfo.Username;
            }

            if (userName == null)
                userName = UserNotIdentified;

            UserName.Content = userName;
        }
    }
}
