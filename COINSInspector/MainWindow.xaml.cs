using COINS_ESB.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;

namespace COINSInspector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        const string _URL = "http://apexcoin.apexpurchasing.com/api/coinsxml";

        private ObservableCollection<CoinsXml> _COINSData;
        public ObservableCollection<CoinsXml> COINSData
        {
            get { return _COINSData; }
            set
            {
                if (value != _COINSData)
                {
                    this._COINSData = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _rawXML;
        public string RawXML
        {
            get { return _rawXML; }
            set
            {
                if (value != _rawXML)
                {
                    this._rawXML = value;
                    NotifyPropertyChanged();
                }
            }

        }

        private string _statusText;
        public string StatusText
        {
            get { return _statusText; }
            set
            {
                if (value != _statusText)
                {
                    this._statusText = value;
                    NotifyPropertyChanged();
                }
            }

        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = this;
            StatusText = "Ready to download COINS data";
        }

        public async Task<IEnumerable<CoinsXml>> GetCoinsXmlAsync(string path)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(path);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                IEnumerable<CoinsXml> coinsxmllist = null;
                HttpResponseMessage response = await client.GetAsync(path);
                if (response.IsSuccessStatusCode)
                {
                    coinsxmllist = await response.Content.ReadAsAsync<IEnumerable<CoinsXml>>();
                }
                return coinsxmllist;
            }
        }

        public string FormatXml(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var stringBuilder = new StringBuilder();
            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true
            };
            doc.Save(XmlWriter.Create(stringBuilder, xmlWriterSettings));
            return stringBuilder.ToString();
        }

        private async void BtnGetCOINSData_Click(object sender, RoutedEventArgs e)
        {
            StatusText = "Downloading COINS data";
            Cursor = Cursors.Wait;
            try
            {
                IEnumerable<CoinsXml> coinsxmllist = await GetCoinsXmlAsync(_URL);
                COINSData = new ObservableCollection<CoinsXml>();
                foreach (CoinsXml coinsxml in coinsxmllist)
                {
                    coinsxml.RawXml = FormatXml(coinsxml.RawXml);
                    COINSData.Add(coinsxml);
                }
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
            StatusText = "COINS data downloaded";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
