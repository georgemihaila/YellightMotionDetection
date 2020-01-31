using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MotionDetection
{
    /// <summary>
    /// Interaction logic for CameraView.xaml
    /// </summary>
    public partial class CameraView : UserControl
    {
        private readonly VideoCaptureDevice _device;
        private int _errorCount = 0;
        private readonly ObservableDictionary<string, object> _infoDictionary;
        private readonly DateTime _lastFrameTime = DateTime.Now;

        public CameraView(VideoCaptureDevice device)
        {
            if (device is null)
                throw new ArgumentNullException(nameof(device));

            InitializeComponent();

            _infoDictionary = new ObservableDictionary<string, object>();
            _infoDictionary.CollectionChanged += _infoDictionary_CollectionChanged;

            _device = device;
            var maxResolution = GetHighestResolutionAvailableForDevice(_device);
            _device.VideoResolution = maxResolution;

            _infoDictionary["Resolution"] = string.Format($"{maxResolution.FrameSize.Width}x{maxResolution.FrameSize.Height}");

            _device.NewFrame += (sender, args) =>
            {
                try
                {
                    Dispatcher.Invoke(() => _device_NewFrame(sender, args));
                }
                catch (TaskCanceledException)
                {
                    //Occurs when terminating the app
                }
            };
            _device.Start();
            _device.VideoSourceError += (sender, args) => Dispatcher.Invoke(() =>
            {
                _errorCount++;
                _infoDictionary["Errors"] = _errorCount.ToString();
                _device.Start();
            });
            HandleTimeoutAsync();
        }

        private void _infoDictionary_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var builder = new StringBuilder();
            foreach (var key in _infoDictionary.Keys)
            {
                builder.Append(key);
                builder.Append(": ");
                builder.Append(_infoDictionary[key]);
                builder.Append('\n');
            }
            InfoTextBlock.Text = builder.ToString();
        }

        private static VideoCapabilities GetHighestResolutionAvailableForDevice(VideoCaptureDevice device) => device.VideoCapabilities.OrderByDescending(x => x.FrameSize.Width * x.FrameSize.Height).First();

        public event EventHandler<Bitmap> NewFrame;

        private void _device_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            NewFrame?.Invoke(this, eventArgs.Frame);
            _infoDictionary["FPS"] = _device.VideoResolution.AverageFrameRate;
            OutputImage.Source = ImageSourceFromBitmap(eventArgs.Frame);
        }

        private async void HandleTimeoutAsync()
        {
            while (true)
            {
                await Task.Delay(100);
                if (DateTime.Now.Subtract(_lastFrameTime).TotalSeconds > 1)
                {
                    _device.Start();
                }
            }
        }

        /// <summary>
        /// Stops the camera.
        /// </summary>
        public void Stop()
        {
        //    _device?.SignalToStop();
            _device?.Stop();
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject([In] IntPtr hObject);

        private static ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                ImageSource newSource = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(handle);
                return newSource;
            }
            catch
            {
                DeleteObject(handle);
                return null;
            }
        }
    }
}
