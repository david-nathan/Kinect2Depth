using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using Microsoft.Kinect;
using Emgu.CV;
using Emgu.CV.Structure;
using System.IO;
namespace Kinect2Depth
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;
        private WriteableBitmap depthBitmap = null;
        private WriteableBitmap colorBitmap = null;
        //DepthImagePixel[] depthPixels;
        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int cbytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        private BitmapSource depthbmSource = null;
        private BitmapSource colorbmSource = null;

        private MultiSourceFrameReader reader = null;
        private ushort[] frameData = null;
        private byte[] depthColorPixels = null;
        private byte[] rgbColorPixels = null;

        int blobCount = 0;

        public MainWindow()
        {

            // initialize the components (controls) of the window
            this.InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.MouseDown += MainWindow_MouseDown;           
        }


        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Only one sensor currently supported
            this.kinectSensor = KinectSensor.Default;

            if (this.kinectSensor != null)
            {
                FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

                // open the sensor
                this.kinectSensor.Open();

                this.reader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

                // allocate space to put the pixels being received and converted
                this.frameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                this.depthColorPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * this.cbytesPerPixel];
                this.rgbColorPixels = new byte[colorFrameDescription.Width * colorFrameDescription.Height * this.bytesPerPixel];

                // create the bitmap to display
                this.depthBitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
                int stride = depthFrameDescription.Width * 4;
                byte[] bits = new byte[depthFrameDescription.Height * stride];
                for (int i = 0; i < bits.Length; i++)
                {
                    bits[i] = 122;
                }
                //create bitmap source to display
                this.depthbmSource = BitmapSource.Create(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null, bits, stride);
                this.colorImg.Source = this.colorBitmap;

                if (this.reader != null)
                {
                    this.reader.MultiSourceFrameArrived += reader_MultiSourceFrameArrived;

                }
            }
            else
            {
                // on failure, set the status text
                this.outputViewbox.Visibility = System.Windows.Visibility.Collapsed;
                this.txtError.Visibility = System.Windows.Visibility.Visible;
                this.txtInfo.Text = Properties.Resources.NoSensorStatusText;

            }
        }

        void reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            blobCount = 0;
            BitmapSource depthBmp = null;
            MultiSourceFrameReference multiReference = e.FrameReference;

            try
            {
                MultiSourceFrame multiFrame = multiReference.AcquireFrame();

                if (multiFrame != null)
                {
                    using (multiFrame)
                    {
                        DepthFrame depthFrame = multiFrame.DepthFrameReference.AcquireFrame();
                        ColorFrame colorFrame = multiFrame.ColorFrameReference.AcquireFrame();


                        if (depthFrame != null)
                        {

                            blobCount = 0;

                            depthBmp = multiFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);

                            Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
                            Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

                            using (MemStorage stor = new MemStorage())
                            {
                                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                                Contour<System.Drawing.Point> contours = gray_image.FindContours(
                                 Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                 Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                                 stor);

                                for (int i = 0; contours != null; contours = contours.HNext)
                                {
                                    i++;

                                    if ((contours.Area > Math.Pow(sliderMinSize.Value, 2)) && (contours.Area < Math.Pow(sliderMaxSize.Value, 2)))
                                    {
                                        MCvBox2D box = contours.GetMinAreaRect();
                                        openCVImg.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
                                        blobCount++;
                                    }
                                }
                            }

                            this.outImg.Source = ImageHelpers.ToBitmapSource(openCVImg);
                            txtBlobCount.Text = blobCount.ToString();
                        }

                        if (colorFrame != null)
                        {

                            if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                            {
                                colorFrame.CopyRawFrameDataToArray(this.rgbColorPixels);
                            }
                            else
                            {
                                colorFrame.CopyConvertedFrameDataToArray(this.rgbColorPixels, ColorImageFormat.Bgra);
                            }
                            
                            this.colorBitmap.WritePixels(
                                new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                                this.rgbColorPixels,
                                this.colorBitmap.PixelWidth * sizeof(int),
                                0);
                        }
                        
                    }

                }               
            }
            catch (Exception err)
            {
                // ignore if the frame is no longer available
                Console.WriteLine(err);
            }
        }

        #region Window Stuff
        void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.reader != null)
            {
                // DepthFrameReder is IDisposable
                this.reader.Dispose();
                this.reader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        private void CloseBtnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion
    }
}
