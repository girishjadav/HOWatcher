using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VOWatcher.model;
using VOAPIService;
using VOAPIService.common;
using System.Configuration;
using Newtonsoft.Json;
using BusinessApp;

namespace VOWatcher
{
    public partial class LoginForm : Form
    {

		private static string dirCSV = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HOWatcher");       //directory of file to import
		public static bool isLogin = false;
		public static UserDetail userDetail = new UserDetail();
		public LoginForm()
        {
            InitializeComponent();
			readSchema();
			if (textBox1.Text.Length > 0)
            {
				VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
				oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
				oAuthParams.Module = "/account/login";
				VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
				//isLogin = oClient.IsLogin(textBox1.Text, textBox2.Text);
				string login = oClient.Login(textBox1.Text, textBox2.Text);
				userDetail = JsonConvert.DeserializeObject<UserDetail>(login);

				if (userDetail != null && userDetail.access_token != null)
                {
					isLogin = true;
				}
				else
                {
					isLogin = false;
                }

			}
		}

		private void writeSchema()
		{


			try
			{
				// Ensure the directory exists
				if (!Directory.Exists(dirCSV))
				{
					Directory.CreateDirectory(dirCSV);
				}

				FileStream fsOutput = new FileStream(dirCSV + "\\schema.ini", FileMode.Create, FileAccess.Write);
				StreamWriter srOutput = new StreamWriter(fsOutput);
				string s1, s2, s3, s4, s5;

				s1 = "[VO Watcher]";
				s2 = "UserName=" + textBox1.Text.ToString();
				s3 = "Password=" + textBox2.Text.ToString();
				s4 = "";
				s5 = "";

				srOutput.WriteLine(s1.ToString() + "\r\n" + s2.ToString() + "\r\n" + s3.ToString() + "\r\n" + s4.ToString() + "\r\n" + s5.ToString());
				srOutput.Close();
				fsOutput.Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "writeSchema");
			}
			finally
			{ }
		}

		private void readSchema()
		{
			bool schemaexit = false;
			string text;

			schemaexit = File.Exists(Path.GetDirectoryName(Application.ExecutablePath) + "\\schema.ini");

			if (schemaexit)
			{
				var fileStream = new FileStream(dirCSV + "\\schema.ini", FileMode.Open, FileAccess.Read);
				//string s3;
				using (var streamReader = new StreamReader(fileStream))
				{

					text = streamReader.ReadToEnd();
					string toBeSearched = "UserName=";
					int ix = text.IndexOf(toBeSearched);
					if (ix != -1)
					{
						string code = text.Substring(ix + toBeSearched.Length);
						foreach (var it in code.Split('\r', '\n'))
						{
							textBox1.Text = it;
							break;
						}

					}

					string toBeSearched1 = "Password=";
					int ix1 = text.IndexOf(toBeSearched1);
					if (ix1 != -1)
					{
						string code = text.Substring(ix1 + toBeSearched1.Length);
						foreach (var it1 in code.Split('\r', '\n'))
						{
							textBox2.Text = it1;
							break;
						}

					}

				}
			}
		}
		private void button2_Click(object sender, EventArgs e)
        {
			this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
			writeSchema();
			if (textBox1.Text.Length > 0)
			{
				VOAPIOAuthParams oAuthParams = new VOAPIOAuthParams();
				oAuthParams.BaseUrl = Convert.ToString(ConfigurationManager.AppSettings["baseurl"].ToString());
				oAuthParams.Module = "/account/login";
				VOApiRestSharp oClient = VOApiRestSharp.GetInstance(oAuthParams);
				isLogin = oClient.IsLogin(textBox1.Text, textBox2.Text);
			}
			if(isLogin)
			{
				MessageBox.Show("Logged in successfully!!!!");
				Form1.isPaused = false;
				this.Close();
			}
		}

        private void textBox1_Validating(object sender, CancelEventArgs e)
        {
			if (string.IsNullOrWhiteSpace(textBox1.Text))
			{
				e.Cancel = true;
				textBox1.Focus();
				errorProviderApp.SetError(textBox1, "UserName should not be left blank!");
			}
			else
			{
				e.Cancel = false;
				errorProviderApp.SetError(textBox1, "");
			}
		}

        private void textBox2_Validating(object sender, CancelEventArgs e)
        {
			if (string.IsNullOrWhiteSpace(textBox2.Text))
			{
				e.Cancel = true;
				textBox2.Focus();
				errorProviderApp.SetError(textBox2, "Password should not be left blank!");
			}
			else
			{
				e.Cancel = false;
				errorProviderApp.SetError(textBox2, "");
			}
		}

        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
			Form1.isPaused = false;
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
