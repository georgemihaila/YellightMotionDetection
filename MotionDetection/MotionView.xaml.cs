using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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

namespace MotionDetection
{
    /// <summary>
    /// Interaction logic for MotionView.xaml
    /// </summary>
    public partial class MotionView : UserControl
    {
        private readonly VideoCaptureDevice _device;
        public string DeviceID { get; private set; }
        private readonly string _deviceConfigurationFileName;
        private Bitmap _previousFrame;
        private System.Drawing.Point _lastDetectionAveragePosition = new System.Drawing.Point(0);
        private MovementDirection _lastDirection = MovementDirection.None;
        private Line _line = new Line()
        {
            Stroke = System.Windows.Media.Brushes.White,
            X1 = 0,
            X2 = 0,
            StrokeEndLineCap = PenLineCap.Triangle,
            StrokeThickness = 10
        };

        GridMotionAreaProcessing _processor;

        Stopwatch directionStopwatch = new Stopwatch();
        int _directionDetectionTime = 300;

        public MotionDetectionConfiguration DetectorConfiguration { get; private set; }
        private MotionDetector _motionDetector;

        public MotionView(VideoCaptureDevice device, string deviceID)
        {
            if (device is null)
                throw new ArgumentNullException(nameof(device));
            if (string.IsNullOrEmpty(deviceID))
                throw new ArgumentNullException(nameof(deviceID));

            DeviceID = deviceID;
            _deviceConfigurationFileName = $"{DeviceID}_config.json";
            if (File.Exists(_deviceConfigurationFileName))
                DetectorConfiguration = JsonConvert.DeserializeObject<MotionDetectionConfiguration>(File.ReadAllText(_deviceConfigurationFileName));
            else
            {
                DetectorConfiguration = new MotionDetectionConfiguration()
                {
                    Size = 4,
                    Sensitivity = 0.03f
                };
            }

            _device = device;

            InitializeComponent();

            _device.NewFrame += (sender, args) => Dispatcher.Invoke(() => _device_NewFrame(sender, args));
            


            DetectionGridContainer.Children.Insert(0, new CameraView(_device));
            SensitivitySlider.Loaded += (s0, e0) =>
            {
                SensitivitySlider.ValueChanged += (s1, e1) =>
                {
                    SensitivitySlider_ValueChanged(s1, e1);
                    ResetDetector();
                };

            };
            DetectionSizeSlider.Loaded += (s0, e0) =>
            {
                DetectionSizeSlider.ValueChanged += DetectionSizeSlider_ValueChanged;
                DetectionSizeSlider.ValueChanged += (s1, e1) =>
                {
                    ResetVisualDetectionGrid();
                    ResetDetector();
                };
            };


            SensitivitySlider.ValueChanged += ConfigurationUpdate;
            DetectionSizeSlider.ValueChanged += ConfigurationUpdate;

            if (DetectorConfiguration != null)
            {
                SensitivitySlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(SensitivitySlider.Value, DetectorConfiguration.Sensitivity));
                DetectionSizeSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(DetectionSizeSlider.Value, DetectorConfiguration.Size));
            }

            ResetVisualDetectionGrid();
            ResetDetector();
            DetectionGridContainer.Children.Add(_line);
            directionStopwatch.Start();
        }

        private void DetectionSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DetectionSizeTextBlock.Text = "Size: " + DetectorConfiguration.Size;
        }

        private void ResetDetector()
        {
            _processor = new GridMotionAreaProcessing(DetectorConfiguration.Size, DetectorConfiguration.Size);
            _processor.MotionAmountToHighlight = DetectorConfiguration.Sensitivity;
            _motionDetector = new MotionDetector(new SimpleBackgroundModelingDetector(), _processor);
        }

        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SensitivityTextBlock.Text = $"Sensitivity: {e.NewValue}";
        }

        private async void _device_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            //detect motion
            if (DetectorConfiguration != null)
            {
                var result = await DetectMotionAsync(_previousFrame, eventArgs.Frame);
                if (result.Direction.X != MovementDirection.None && result.Direction.Y != MovementDirection.None)
                    MotionDetected?.Invoke(this, result);
            }
            _previousFrame = eventArgs.Frame;
        }

        private async Task<MotionDetectionResult> DetectMotionAsync(Bitmap previousFrame, Bitmap currentFrame)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var result = _motionDetector.ProcessFrame(currentFrame);
            stopwatch.Stop();

            MovementDirection xDirection = MovementDirection.None;
            MovementDirection yDirection = MovementDirection.None;
            if (result > DetectorConfiguration.Sensitivity)
            {
                var motionZonesList = new List<System.Drawing.Rectangle>();
                for (int x = 0, i = 0; x < _processor.MotionGrid.GetLength(0); x++)
                {
                    for (int y = 0; y < _processor.MotionGrid.GetLength(1); y++, i++)
                    {
                        if (_processor.MotionGrid[x, y] <= _processor.MotionAmountToHighlight)
                            continue;
                        motionZonesList.Add(new System.Drawing.Rectangle(new System.Drawing.Point(x, y), new System.Drawing.Size(new System.Drawing.Point(0))));
                    }
                }
                _motionDetector.MotionZones = motionZonesList.ToArray();
                if (_motionDetector.MotionZones != null && _motionDetector.MotionZones.Length > 0)
                {
                    var currentAverage = new System.Drawing.Point((int)_motionDetector.MotionZones.Average(x => x.Y), (int)_motionDetector.MotionZones.Average(x => x.X));
                    {
                        if (currentAverage.X < _lastDetectionAveragePosition.X)
                        {
                            xDirection = MovementDirection.Left;
                        }
                        else
                        {
                            xDirection = MovementDirection.Right;
                        }
                    }
                    if (currentAverage.Y < _lastDetectionAveragePosition.Y)
                    {
                        yDirection = MovementDirection.Up;
                    }
                    else
                    {
                        yDirection = MovementDirection.Down;
                    }
                    
                    if (directionStopwatch.Elapsed.TotalMilliseconds > _directionDetectionTime)
                    {
                        _line.X1 = _lastDetectionAveragePosition.X * DetectionGrid.ActualWidth / DetectorConfiguration.Size;
                        _line.Y1 = _lastDetectionAveragePosition.Y * DetectionGrid.ActualHeight / DetectorConfiguration.Size;
                        directionStopwatch.Restart();
                        ResetDetector();
                    }

                    _lastDetectionAveragePosition = currentAverage;
                    _lastDirection = xDirection;

                    _line.X2 = _lastDetectionAveragePosition.X * DetectionGrid.ActualWidth/ DetectorConfiguration.Size;
                    _line.Y2 = _lastDetectionAveragePosition.Y * DetectionGrid.ActualHeight / DetectorConfiguration.Size;
                    ;
                    XDirectionTextBlock.Text = xDirection.ToString();
                    YDirectionTextBlock.Text = yDirection.ToString();
                }
                ResetDetector();
            }
            return new MotionDetectionResult()
            {
                Direction = (xDirection, yDirection),
                DetectionTimeMilliseconds = stopwatch.Elapsed.TotalMilliseconds
            };
        }

        private void ResetVisualDetectionGrid()
        {
            DetectionGrid.ColumnDefinitions.Clear();
            for (int i = 0; i < DetectorConfiguration.Size; i++)
            {
                DetectionGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            DetectionGrid.RowDefinitions.Clear();
            for (int i = 0; i < DetectorConfiguration.Size; i++)
            {
                DetectionGrid.RowDefinitions.Add(new RowDefinition());
            }
        }

        public event EventHandler<MotionDetectionResult> MotionDetected;

        private void ConfigurationUpdate(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DetectorConfiguration = new MotionDetectionConfiguration()
            {
                Sensitivity = (float)SensitivitySlider.Value,
                Size = Convert.ToInt32(DetectionSizeSlider.Value)
            };
            File.WriteAllText(_deviceConfigurationFileName, JsonConvert.SerializeObject(DetectorConfiguration));
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetDetector();
        }
    }
}
