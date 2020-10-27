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
using System.Windows.Shapes;

namespace ApexCOINESB
{
    /// <summary>
    /// Interaction logic for COINSWBS.xaml
    /// </summary>
    public partial class COINSWBS : Window
    {
        ApexDataDataContext dc = new ApexDataDataContext();

        public COINSWBS()
        {
            InitializeComponent();
            grdWBS.ItemsSource = dc.COINSESB_WBs;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            grdWBS.View.CommitEditing();
            dc.SubmitChanges();
        }

        private void btnDiscard_Click(object sender, RoutedEventArgs e)
        {
            dc = new ApexDataDataContext();
            grdWBS.ItemsSource = dc.COINSESB_WBs;
            MessageBox.Show("Changes discarded");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
