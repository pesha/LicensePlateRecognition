using AForge.Neuro;
using LicensePlateRecognition;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
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

namespace LicensePlateRecognitionExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string _image;
        private LicensePlateDetector _detector;
        
        public MainWindow()
        {
            InitializeComponent();

            ActivationNetwork net = (ActivationNetwork)Network.Load("net-numbers");
            ActivationNetwork netLetters = (ActivationNetwork)Network.Load("net-lt");
            _detector = new LicensePlateDetector(net, netLetters);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.DefaultExt = ".jpg"; // Required file extension 
            fileDialog.Filter = "Obrázky (.jpg)|*.jpg"; // Optional file extensions


            Nullable<bool> result = fileDialog.ShowDialog();

            if (result == true)
            {
                string filename = fileDialog.FileName;
                fileNameTextBox.Text = filename;
                _image = filename;
                this.DetectButton.IsEnabled = true;
            }

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Bitmap image = AForge.Imaging.Image.FromFile(this._image);

            List<string> data = _detector.DetectLicensePlate(image);

            if (data.Count > 0)
            {
                this.LpTextLabel.Content = data.First();
            }
        }


    }
}
