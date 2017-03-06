using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using TrackBallTest.UserControls;

namespace TrackBallTest
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Define TrackPort
        private Trackball _trackball;
        private System.Windows.Media.TransformGroup _transformGroup = new System.Windows.Media.TransformGroup();
        private System.Windows.Media.TranslateTransform _translateTransform = new System.Windows.Media.TranslateTransform(0, 0);

        private System.Windows.Point _previousPosition2D;
        private Vector3D _previousPosition3D = new Vector3D(0, 0, 1);

        private bool IsMouseDown = false;

        #endregion

        #region TrackPort

        void CaptureBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = true;

            Mouse.Capture(CaptureBorder, CaptureMode.Element);
            _previousPosition2D = e.GetPosition(CaptureBorder);
            _previousPosition3D = ProjectToTrackball(
                CaptureBorder.ActualWidth,
                CaptureBorder.ActualHeight,
                _previousPosition2D);

            txtLog.AppendText("MouseDown: " + _previousPosition2D.X + ", " + _previousPosition2D.Y + "\r\n");
            txtLog.ScrollToEnd();
        }

        void CaptureBorder_MouseMove(object sender, MouseEventArgs e)
        {

            if(!IsMouseDown)
            {
                //CaptureBorder_MouseDown(sender, e);
                return;
            }

            System.Windows.Point currentPosition = e.GetPosition(CaptureBorder);

            if (currentPosition == _previousPosition2D)
            {
                return;
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                Translate(currentPosition);
            }

            _previousPosition2D = currentPosition;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                txtLog.AppendText("MouseMove: " + _previousPosition2D.X + ", " + _previousPosition2D.Y + "\r\n");
                txtLog.ScrollToEnd();
            }
        }

        void CaptureBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = false;

            Mouse.Capture(CaptureBorder, CaptureMode.None);
            _previousPosition2D = e.GetPosition(CaptureBorder);

            txtLog.AppendText("MouseUp: " + _previousPosition2D.X + ", " + _previousPosition2D.Y + "\r\n");
            txtLog.ScrollToEnd();
        }

        private void Translate(System.Windows.Point currentPosition)
        {
            Vector3D currentPosition3D = ProjectToTrackball(
               CaptureBorder.ActualWidth, CaptureBorder.ActualHeight, currentPosition);

            double moveX = _previousPosition3D.X - currentPosition3D.X;
            double moveY = _previousPosition3D.Y - currentPosition3D.Y;

            //TranslateTransform3D itemTrans = new TranslateTransform3D();
            PerspectiveCamera trans = (viewport3dRoot.Camera) as PerspectiveCamera;

            trans.Position = new Point3D(trans.Position.X + moveX * 1000, trans.Position.Y + moveY * 1000, trans.Position.Z);

            _previousPosition2D = currentPosition;
            _previousPosition3D = currentPosition3D;
        }

        private Vector3D ProjectToTrackball(double width, double height, System.Windows.Point point)
        {
            double x = point.X / (width / 2);    // Scale so bounds map to [0,0] - [2,2]
            double y = point.Y / (height / 2);

            x = x - 1;                           // Translate 0,0 to the center
            y = 1 - y;                           // Flip so +Y is up instead of down

            double z2 = 1 - x * x - y * y;       // z^2 = 1 - x^2 - y^2
            double z = z2 > 0 ? Math.Sqrt(z2) : 0;

            return new Vector3D(x, y, z);
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            #region trackball
            _trackball = new Trackball(this.Camera, this);
            this.Camera.Transform = _trackball.Transform;
            this.RenderTransform = _transformGroup;
            this.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            _transformGroup.Children.Add(_translateTransform);

            CaptureBorder.MouseDown += CaptureBorder_MouseDown;
            CaptureBorder.MouseUp += CaptureBorder_MouseUp;
            CaptureBorder.MouseMove += CaptureBorder_MouseMove;

            _trackball.EventSource = CaptureBorder;
            #endregion
        }
    }
}
