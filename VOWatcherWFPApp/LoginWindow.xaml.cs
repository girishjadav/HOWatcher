using Newtonsoft.Json;
using RestSharp;
using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.UI.WebControls;
using System.Windows;
using VOWatcherWFPApp.model;

namespace VOWatcherWFPApp
{
    public partial class LoginWindow : Window
    {
        private static string dirCSV = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        public static bool isLogin = false;
        public static UserDetail userDetail = new UserDetail();

        private string savedUsername = "";
        private string savedPassword = "";
        public static event Action OnLogout;


        public LoginWindow()
        {
            InitializeComponent();

            // Try auto-login on window load
            Loaded += LoginWindow_Loaded;
        }

        public static void PerformLogout()
        {
            isLogin = false;
            userDetail = null;

            // Notify all subscribers (e.g., other windows)
            OnLogout?.Invoke();
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ReadSchema())
            {
                var result = MessageBox.Show(
                    $"Found saved credentials for '{savedUsername}'. Do you want to login?",
                    "Auto Login",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Silent login
                    DoLogin(savedUsername, savedPassword, silent: true);

                    if (isLogin) // ✅ only if login succeeded
                    {
                        var tracker = Application.Current.MainWindow as Tracker;
                        if (tracker != null)
                        {
                            tracker.LoadTrackerData(); // ✅ Call only after login success
                        }

                        this.Close(); // ✅ close after everything
                    }
                   
                }
                else
                {
                    // user wants to login manually
                    this.Visibility = Visibility.Visible;
                    UsernameTextBox.Text = savedUsername;
                    PasswordBox.Password = savedPassword;
                }
            }
            else
            {
                // no saved credentials
                this.Visibility = Visibility.Visible;
            }
        }



        private void LoginOrLogout_Click(object sender, RoutedEventArgs e)
        {
            if (!isLogin)
            {
                // Try manual login
                DoLogin(UsernameTextBox.Text, PasswordBox.Password);
            }
            else
            {
                var result = System.Windows.MessageBox.Show("Do you want to logout?", "Confirm Logout", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    DoLogout();
                }
            }
        }

        private void DoLogin(string username, string password, bool silent = false)
        {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                string baseUrl = ConfigurationManager.AppSettings["baseurl"];
                string module = "/account/login";
                string fullUrl = baseUrl.TrimEnd('/') + module;

                var client = new RestClient(fullUrl);
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/json");

                var credentials = new { email = username, password = password };
                string jsonBody = JsonConvert.SerializeObject(credentials);
                request.AddParameter("application/json", jsonBody, ParameterType.RequestBody);

                var response = client.Execute(request);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    userDetail = JsonConvert.DeserializeObject<UserDetail>(response.Content);

                    if (userDetail != null && !string.IsNullOrEmpty(userDetail.access_token))
                    {
                        isLogin = true;
                        WriteSchema(username, password);

                        if (!silent)
                            System.Windows.MessageBox.Show("Logged in successfully!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                        btnLogin.Content = "Logout";


                        if (this.IsLoaded && this.IsVisible)
                        {
                            this.DialogResult = true;
                        }
                        else
                        {
                            this.Close();
                        }
                    }
                }

                if (!silent)
                    System.Windows.MessageBox.Show("Login failed. Check credentials or server response.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            else
            {
                if (!silent)
                    System.Windows.MessageBox.Show("Username and Password are required.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }


        }


        private void DoLogout()
        {
            isLogin = false;

            // DO NOT DELETE schema.ini
            UsernameTextBox.Text = "";
            PasswordBox.Password = "";

            System.Windows.MessageBox.Show("Logged out successfully.", "Logout", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            btnLogin.Content = "Login";

        }


        private bool ReadSchema()
        {
            try
            {
                string filePath = Path.Combine(dirCSV, "schema.ini");
                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("UserName="))
                            savedUsername = line.Substring("UserName=".Length);
                        else if (line.StartsWith("Password="))
                        {
                            string encryptedPassword = line.Substring("Password=".Length);
                            try
                            {
                                savedPassword = DecryptPassword(encryptedPassword);
                            }
                            catch
                            {
                                savedPassword = string.Empty;
                            }
                        }
                    }

                    return !string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error reading schema.ini: {ex.Message}", "File Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            return false;
        }

        private void WriteSchema(string username, string password)
        {
            try
            {
                string filePath = Path.Combine(dirCSV, "schema.ini");
                string encryptedPassword = EncryptPassword(password);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("[VO Watcher]");
                    writer.WriteLine($"UserName={username}");
                    writer.WriteLine($"Password={encryptedPassword}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error writing schema.ini: {ex.Message}", "File Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private string EncryptPassword(string plainText)
        {
            byte[] key = Encoding.UTF8.GetBytes("9x@2Lk!fQ7$e1TpM");
            byte[] iv = Encoding.UTF8.GetBytes("u!9Rm4YcB#6zKdPw");

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs))
                        sw.Write(plainText);

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private string DecryptPassword(string cipherText)
        {
            byte[] key = Encoding.UTF8.GetBytes("9x@2Lk!fQ7$e1TpM");
            byte[] iv = Encoding.UTF8.GetBytes("u!9Rm4YcB#6zKdPw");

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                byte[] buffer = Convert.FromBase64String(cipherText);
                using (MemoryStream ms = new MemoryStream(buffer))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs))
                    return sr.ReadToEnd();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
