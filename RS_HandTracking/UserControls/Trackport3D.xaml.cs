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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RS_HandTracking.UserControls
{
    /// <summary>
    /// Trackport3D.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Trackport3D : UserControl
    {
        private Trackball _trackball = null;

        private System.Windows.Media.TransformGroup _transformGroup = new System.Windows.Media.TransformGroup();
        private System.Windows.Media.TranslateTransform _translateTransform = new System.Windows.Media.TranslateTransform(0, 0);

        private Point _previousPosition2D;
        private Vector3D _previousPosition3D = new Vector3D(0, 0, 1);

        private DispatcherTimer dTimer;

        private bool IsDebug = false;

        public Trackport3D()
        {
            InitializeComponent();

            InitControl();
        }

        private void layoutRoot_Loaded(object sender, RoutedEventArgs e)
        {
            _trackball.EventSource = CaptureBorder;
        }

        private void InitControl()
        {
            this.Camera.Transform = _trackball.Transform;
            this.RenderTransform = _transformGroup;
            this.RenderTransformOrigin = new Point(0.5, 0.5);

            _transformGroup.Children.Add(_translateTransform);

            CaptureBorder.MouseDown += CaptureBorder_MouseDown;
            CaptureBorder.MouseUp += CaptureBorder_MouseUp;
            CaptureBorder.MouseMove += CaptureBorder_MouseMove;

#if DEBUG
            dTimer = new DispatcherTimer();

            dTimer.Interval = new TimeSpan(0, 0, 10);

            dTimer.Tick += dTimer_Tick;

            dTimer.Start();
#endif
        }

        void dTimer_Tick(object sender, EventArgs e)
        {
            IsDebug = true;
            dTimer.Stop();
        }

        void CaptureBorder_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPosition = e.GetPosition(CaptureBorder);

            if (currentPosition == _previousPosition2D)
            {
                return;
            }

            // Prefer tracking to zooming if both buttons are pressed.
            if (e.RightButton == MouseButtonState.Pressed)
            {
                Translate(currentPosition);
            }

            _previousPosition2D = currentPosition;
        }

        void CaptureBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(CaptureBorder, CaptureMode.None);
        }

        void CaptureBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(CaptureBorder, CaptureMode.Element);
            _previousPosition2D = e.GetPosition(CaptureBorder);
            _previousPosition3D = ProjectToTrackball(
                CaptureBorder.ActualWidth,
                CaptureBorder.ActualHeight,
                _previousPosition2D);
        }

        private void Translate(Point currentPosition)
        {
#if false
            double moveX = _previousPosition2D.X - currentPosition.X;
            double moveY = _previousPosition2D.Y - currentPosition.Y;

            _translateTransform.X = _translateTransform.X - moveX;
            _translateTransform.Y = _translateTransform.Y - moveY;

            _previousPosition2D = currentPosition;
#else

            Vector3D currentPosition3D = ProjectToTrackball(
               CaptureBorder.ActualWidth, CaptureBorder.ActualHeight, currentPosition);

            double moveX = _previousPosition3D.X - currentPosition3D.X;
            double moveY = _previousPosition3D.Y - currentPosition3D.Y;

            TranslateTransform3D itemTrans = new TranslateTransform3D();
            PerspectiveCamera trans = (viewport3dRoot.Camera) as PerspectiveCamera;

            trans.Position = new Point3D(trans.Position.X + moveX, trans.Position.Y + moveY, trans.Position.Z);

            _previousPosition2D = currentPosition;
            _previousPosition3D = currentPosition3D;
#endif
        }

        private Vector3D ProjectToTrackball(double width, double height, Point point)
        {
            double x = point.X / (width / 2);    // Scale so bounds map to [0,0] - [2,2]
            double y = point.Y / (height / 2);

            x = x - 1;                           // Translate 0,0 to the center
            y = 1 - y;                           // Flip so +Y is up instead of down

            double z2 = 1 - x * x - y * y;       // z^2 = 1 - x^2 - y^2
            double z = z2 > 0 ? Math.Sqrt(z2) : 0;

            return new Vector3D(x, y, z);
        }


    }
}
