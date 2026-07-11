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

namespace WinDama
{
    /// <summary>
    /// Logique d'interaction pour CustomMessageBox.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {

        public CustomMessageBox(string message, string title)
        {
            InitializeComponent();
            this.Title = title;
            this.MessageTextBlock.Text = message;
        }

        public static void Show(string message, string title, Point location)
        {
            CustomMessageBox customMessageBox = new CustomMessageBox(message, title);
            customMessageBox.WindowStartupLocation = WindowStartupLocation.Manual;
            customMessageBox.Left = location.X;
            customMessageBox.Top = location.Y;
            customMessageBox.ShowDialog();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

}
