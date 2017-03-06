using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace RS_HandTracking.UserControls
{
    public class Trackball
    {
        private FrameworkElement _eventSource;
        private Point _previousPosition2D;
        private Vector3D _previousPosition3D = new Vector3D(0, 0, 1);

        private Transform3DGroup _transform3D;
        private ScaleTransform3D _scale3D = new ScaleTransform3D();
        private AxisAngleRotation3D _rotation3D = new AxisAngleRotation3D();
        private PerspectiveCamera Camera = null;
        private MainWindow MW = null;

        private bool IsZoom = false;

        public Trackball(PerspectiveCamera _Camera, MainWindow _MW)
        {
            Camera = _Camera;
            _transform3D = new Transform3DGroup();
            _transform3D.Children.Add(_scale3D);
            _transform3D.Children.Add(new RotateTransform3D(_rotation3D));

            this.MW = _MW;
        }

        /// <summary>
        ///     A transform to move the camera or scene to the trackball's
        ///     current orientation and scale.
        /// </summary>
        public Transform3D Transform
        {
            get { return _transform3D; }
        }

        #region Event Handling

        /// <summary>
        ///     The FrameworkElement we listen to for mouse events.
        /// </summary>
        public FrameworkElement EventSource
        {
            get { return _eventSource; }

            set
            {
                if (_eventSource != null)
                {
                    _eventSource.MouseDown -= this.OnMouseDown;
                    _eventSource.MouseUp -= this.OnMouseUp;
                    _eventSource.MouseMove -= this.OnMouseMove;

                    _eventSource.MouseWheel -= this.OnMouseWheel;
                }

                _eventSource = value;

                _eventSource.MouseDown += this.OnMouseDown;
                _eventSource.MouseUp += this.OnMouseUp;
                _eventSource.MouseMove += this.OnMouseMove;

                _eventSource.MouseWheel += this.OnMouseWheel;
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            Mouse.Capture(EventSource, CaptureMode.Element);
            _previousPosition2D = e.GetPosition(EventSource);
            _previousPosition3D = ProjectToTrackball(
                EventSource.ActualWidth,
                EventSource.ActualHeight,
                _previousPosition2D);
        }

        public void OnMouseDownPublic(object sender, MouseEventArgs e, Point point)
        {
            //Mouse.Capture(EventSource, CaptureMode.Element);
            _previousPosition2D = point;
            _previousPosition3D = ProjectToTrackball(
                EventSource.ActualWidth,
                EventSource.ActualHeight,
                _previousPosition2D);
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            Mouse.Capture(EventSource, CaptureMode.None);
        }

        public void OnMouseUpPublic(object sender, MouseEventArgs e)
        {
            Mouse.Capture(EventSource, CaptureMode.None);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point currentPosition = e.GetPosition(EventSource);

            // Prefer tracking to zooming if both buttons are pressed.
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Track(currentPosition);
            }

            _previousPosition2D = currentPosition;
        }

        public void OnMouseMovePublic(object sender, MouseEventArgs e, Point point)
        {
            Point currentPosition = point;

            // Prefer tracking to zooming if both buttons are pressed.
            //if (e.LeftButton == MouseButtonState.Pressed)
            {
                Track(currentPosition);
            }

            _previousPosition2D = currentPosition;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Zoom(e.Delta * 0.1, 1, 10);
        }

#endregion Event Handling

        private void Track(Point currentPosition)
        {
            try
            {
                Vector3D currentPosition3D = ProjectToTrackball(
                    EventSource.ActualWidth, EventSource.ActualHeight, currentPosition);

                Vector3D axis = Vector3D.CrossProduct(_previousPosition3D, currentPosition3D);
                double angle = Vector3D.AngleBetween(_previousPosition3D, currentPosition3D);
                Quaternion delta;

                try
                {
                    delta = new Quaternion(axis, -angle);
                }
                catch (Exception ex)
                {
                    return;
                }

                // Get the current orientantion from the RotateTransform3D
                AxisAngleRotation3D r = _rotation3D;
                Quaternion q = new Quaternion(_rotation3D.Axis, _rotation3D.Angle);

                // Compose the delta with the previous orientation
                q *= delta;

                // Write the new orientation back to the Rotation3D
                _rotation3D.Axis = q.Axis;
                _rotation3D.Angle = q.Angle;

                _previousPosition3D = currentPosition3D;
            }
            catch (Exception ex)
            {
                string err = ex.Message + "\r\n" + ex.StackTrace;

                MessageBox.Show(err);
            }
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

        private void Zoom(Point currentPosition)
        {
            double yDelta = currentPosition.Y - _previousPosition2D.Y;

            double scale = Math.Exp(yDelta / 100);    // e^(yDelta/100) is fairly arbitrary.

            _scale3D.ScaleX *= scale;
            _scale3D.ScaleY *= scale;
            _scale3D.ScaleZ *= scale;
        }

        public void Zoom(double delta, double multiplyFactor, int Millisecond)
        {
            double scale = Math.Exp(delta * multiplyFactor / 100);    // e^(yDelta/100) is fairly arbitrary.

#if false
            _scale3D.ScaleX = _scale3D.ScaleX * scale;
            _scale3D.ScaleY = _scale3D.ScaleY * scale;
            _scale3D.ScaleZ = _scale3D.ScaleZ * scale;
#else
            if (IsZoom == true)
            {
                return;
            }
            else
            {
                IsZoom = true;
            }

            //SetScaleTransform(_scale3D.ScaleX, _scale3D.ScaleY, _scale3D.ScaleZ, scale, 0, 1000);

            double x = ((ScaleTransform3D)((Transform3DGroup)Camera.Transform).Children[0]).ScaleX;
            double y = ((ScaleTransform3D)((Transform3DGroup)Camera.Transform).Children[0]).ScaleY;
            double z = ((ScaleTransform3D)((Transform3DGroup)Camera.Transform).Children[0]).ScaleZ;

            SetScaleTransform(x, y, z, scale, 0, Millisecond);
#endif
        }

        public void Zoom(double delta, double multiplyFactor)
        {
            double scale = Math.Exp(delta * multiplyFactor / 100);    // e^(yDelta/100) is fairly arbitrary.

            _scale3D.ScaleX = _scale3D.ScaleX * scale;
            _scale3D.ScaleY = _scale3D.ScaleY * scale;
            _scale3D.ScaleZ = _scale3D.ScaleZ * scale;
        }

        public void SetScaleTransform(double oldX, double oldY, double oldZ, double delta, int start, int end)
        {
            try
            {
                Storyboard sb = new Storyboard();

                sb.Completed += sb_Completed;

                sb.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(end));

#region X 축 이동
                DoubleAnimationUsingKeyFrames daKFX = new DoubleAnimationUsingKeyFrames();

                KeyTime ktStartX = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, start));
                KeyTime ktEndX = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, end));

                EasingDoubleKeyFrame edkfStartX = new EasingDoubleKeyFrame(oldX, ktStartX);
                EasingDoubleKeyFrame edkfEndX = new EasingDoubleKeyFrame(oldX * delta, ktEndX);

                daKFX.KeyFrames.Add(edkfStartX);
                daKFX.KeyFrames.Add(edkfEndX);

                sb.Children.Add(daKFX);
                Storyboard.SetTargetProperty(daKFX, new PropertyPath("(Camera.Transform).(Transform3DGroup.Children)[0].(ScaleTransform3D.ScaleX)"));
                //Storyboard.SetTarget(daKFX, Camera);
                Storyboard.SetTargetName(daKFX, "Camera");
#endregion

#region Y 축 이동
                DoubleAnimationUsingKeyFrames daKFY = new DoubleAnimationUsingKeyFrames();

                KeyTime ktStartY = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, start));
                KeyTime ktEndY = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, end));

                EasingDoubleKeyFrame edkfStartY = new EasingDoubleKeyFrame(oldY, ktStartY);
                EasingDoubleKeyFrame edkfEndY = new EasingDoubleKeyFrame(oldY * delta, ktEndY);

                daKFY.KeyFrames.Add(edkfStartY);
                daKFY.KeyFrames.Add(edkfEndY);

                sb.Children.Add(daKFY);
                Storyboard.SetTargetProperty(daKFY, new PropertyPath("(Camera.Transform).(Transform3DGroup.Children)[0].(ScaleTransform3D.ScaleY)"));
                //Storyboard.SetTarget(daKFY, Camera);
                Storyboard.SetTargetName(daKFY, "Camera");
#endregion

#region Z 축 이동
                DoubleAnimationUsingKeyFrames daKFZ = new DoubleAnimationUsingKeyFrames();

                KeyTime ktStartZ = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, start));
                KeyTime ktEndZ = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, end));

                EasingDoubleKeyFrame edkfStartZ = new EasingDoubleKeyFrame(oldZ, ktStartZ);
                EasingDoubleKeyFrame edkfEndZ = new EasingDoubleKeyFrame(oldZ * delta, ktEndZ);

                daKFZ.KeyFrames.Add(edkfStartZ);
                daKFZ.KeyFrames.Add(edkfEndZ);

                sb.Children.Add(daKFZ);
                Storyboard.SetTargetProperty(daKFZ, new PropertyPath("(Camera.Transform).(Transform3DGroup.Children)[0].(ScaleTransform3D.ScaleZ)"));
                //Storyboard.SetTarget(daKFZ, Camera);
                Storyboard.SetTargetName(daKFZ, "Camera");
#endregion

                sb.Begin(MW);
            }
            catch (Exception ex)
            {
                string err = ex.Message + "\r\n" + ex.StackTrace;
                MessageBox.Show(err);
            }
        }

        void sb_Completed(object sender, EventArgs e)
        {
            IsZoom = false;
        }
    }
}
