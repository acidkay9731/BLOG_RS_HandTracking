using RS_HandTracking.UserControls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
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

namespace RS_HandTracking
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public PXCMSession g_session;
        private Bitmap bitmap = null;
        public Dictionary<string, PXCMCapture.DeviceInfo> Devices { get; set; }
        private PXCMCapture.Handler _deviceChangeHandler;
        private PXCMCapture _capture;
        public volatile bool stop = false;
        HandsRecognition gr;

        bool IsLabel = true;

        bool IsCursor = false;

        bool IsAdaptive = false;

        private float pSize = 3.0f;

        //ArrayList arrHandPoint = new ArrayList();
        System.Windows.Point oldAdaptiveHandPoint = new System.Windows.Point();
        System.Windows.Point adaptiveHandPoint = new System.Windows.Point();

        System.Windows.Point oldCursorHandPoint = new System.Windows.Point();
        System.Windows.Point cursorHandPoint = new System.Windows.Point();

        System.Windows.Point oldHandPoint = new System.Windows.Point();
        System.Windows.Point HandPoint = new System.Windows.Point();

        #region Define TrackPort
        private Trackball _trackball;
        private System.Windows.Media.TransformGroup _transformGroup = new System.Windows.Media.TransformGroup();
        private System.Windows.Media.TranslateTransform _translateTransform = new System.Windows.Media.TranslateTransform(0, 0);

        private System.Windows.Point _previousPosition2D;
        private Vector3D _previousPosition3D = new Vector3D(0, 0, 1);

        ushort[] DepthMap;

        public ushort oldDepth = 0;

        #endregion

        #region TrackPort

        void CaptureBorder_MouseMove(object sender, MouseEventArgs e)
        {
            
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
        }

        void CaptureBorder_MouseMove(object sender, MouseEventArgs e, System.Windows.Point currentPosition)
        {
            if (currentPosition == _previousPosition2D)
            {
                return;
            }

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

        public void SetDepth(ushort[] dpixels)
        {
            DepthMap = dpixels.Clone() as ushort[];
        }

        void CaptureBorder_MouseDown(object sender, MouseButtonEventArgs e, System.Windows.Point point)
        {
            Mouse.Capture(CaptureBorder, CaptureMode.Element);
            _previousPosition2D = point;
            _previousPosition3D = ProjectToTrackball(
                CaptureBorder.ActualWidth,
                CaptureBorder.ActualHeight,
                _previousPosition2D);
        }

        public void CaptureBorder_MouseUp(bool IsLeft)
        {
            if(IsLeft)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                    MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Left);

                _trackball.OnMouseUpPublic(null, mouseEvent);
                });
            }
            else
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                    MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Right);
                    _trackball.OnMouseUpPublic(null, mouseEvent);
                });
            }
        }

        public void CaptureBorder_MouseDown(bool IsLeft)
        {
            if(IsLeft)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                    MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Left);
                    
                    if(IsCursor)
                    {
                        if (IsAdaptive)
                        {
                            _trackball.OnMouseDownPublic(null, mouseEvent, adaptiveHandPoint);
                        }
                        else
                        {
                            _trackball.OnMouseDownPublic(null, mouseEvent, cursorHandPoint);
                        }
                    }
                    else
                    {
                        _trackball.OnMouseDownPublic(null, mouseEvent, HandPoint);
                    }
                    
                });
            }
            else
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                    MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Right);

                    if (IsCursor)
                    {
                        _trackball.OnMouseDownPublic(null, mouseEvent, adaptiveHandPoint);
                    }
                    else
                    {
                        _trackball.OnMouseDownPublic(null, mouseEvent, HandPoint);
                    }
                });
            }
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

            g_session = null;
            g_session = PXCMSession.CreateInstance();

            if (g_session == null)
            {
                MessageBox.Show("Session is null");
                this.Close();
            }

            _deviceChangeHandler = new PXCMCapture.Handler { onDeviceListChanged = OnDeviceListChanged };

            PopulateDeviceMenu();

            _trackball = new Trackball(this.Camera, this);
            this.Camera.Transform = _trackball.Transform;
            this.RenderTransform = _transformGroup;
            this.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            _transformGroup.Children.Add(_translateTransform);

            CaptureBorder.MouseDown += CaptureBorder_MouseDown;
            CaptureBorder.MouseUp += CaptureBorder_MouseUp;
            CaptureBorder.MouseMove += CaptureBorder_MouseMove;

            _trackball.EventSource = CaptureBorder;

            InitControl();

            gr = new HandsRecognition(this, IsLabel, IsCursor);
        }

        private void InitControl()
        {
            chkLabel.Checked += ChkLabel_Checked;
            chkLabel.Unchecked += ChkLabel_Unchecked;

            chkDepth.Checked += ChkDepth_Checked;
            chkDepth.Unchecked += ChkDepth_Unchecked;

            rbCursor.Checked += RbCursor_Checked;
            rbSkeleton.Checked += RbSkeleton_Checked;
        }

        private void ChkDepth_Unchecked(object sender, RoutedEventArgs e)
        {
            IsAdaptive = false;
        }

        private void ChkDepth_Checked(object sender, RoutedEventArgs e)
        {
            IsAdaptive = true;
        }

        private void RbCursor_Checked(object sender, RoutedEventArgs e)
        {
            IsCursor = true;
            gr.IsCursor = true;

            chkDepth.IsEnabled = true;
            chkLabel.IsEnabled = false;
        }

        private void RbSkeleton_Checked(object sender, RoutedEventArgs e)
        {
            IsCursor = false;
            gr.IsCursor = false;

            chkDepth.IsEnabled = false;
            chkLabel.IsEnabled = true;
        }
        
        private void ChkLabel_Checked(object sender, RoutedEventArgs e)
        {
            IsLabel = true;
        }

        private void ChkLabel_Unchecked(object sender, RoutedEventArgs e)
        {
            IsLabel = false;
        }

        private void workerXaml_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        void OnDeviceListChanged()
        {
            PopulateDeviceMenu();
            UpdateStatus("Device plugged/unplugged");
        }

        private void PopulateDeviceMenu()
        {
            Devices = new Dictionary<string, PXCMCapture.DeviceInfo>();

            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc
            {
                @group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };

            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (g_session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                if (g_session.CreateImpl<PXCMCapture>(desc1, out _capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;
                _capture.SubscribeToCaptureCallbacks(_deviceChangeHandler);
                
                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (_capture.QueryDeviceInfo(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                    string name = dinfo.name;
                    if (Devices.ContainsKey(dinfo.name))
                    {
                        name += j;
                    }
                    Devices.Add(name, dinfo);
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (btnStart.Content.ToString() == "START")
            {
                btnStart.Content = "STOP";

                bitmap = null;

                System.Threading.Thread thread = new System.Threading.Thread(DoRecognition);
                thread.Start();
                System.Threading.Thread.Sleep(5);

                this.stop = false;

                if(IsCursor)
                {
                    chkDepth.IsEnabled = false;
                }
                else
                {
                    chkLabel.IsEnabled = false;
                }

                rbCursor.IsEnabled = false;
                rbSkeleton.IsEnabled = false;
            }
            else
            {

                btnStart.Content = "START";

                this.stop = true;

                if (IsCursor)
                {
                    chkDepth.IsEnabled = true;
                }
                else
                {
                    chkLabel.IsEnabled = true;
                }

                rbCursor.IsEnabled = true;
                rbSkeleton.IsEnabled = true;
            }
        }

        delegate void DoRecognitionCompleted();
        private void DoRecognition()
        {
            gr = new HandsRecognition(this, IsLabel, IsCursor);
            gr.SimplePipeline();
        }

        internal void UpdateStatus(string status)
        {
            Status2.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                Status2.Text = status;
            });
        }

        public void UpdateInfo(string status)
        {
            infoTextBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                infoTextBox.AppendText(status);
                infoTextBox.ScrollToEnd();
            });
        }

        public void UpdateFPSStatus(string status)
        {
            labelFPS.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
           {
               labelFPS.Text = status;
           });
        }

        public void DisplayBitmap(Bitmap picture)
        {
            lock (this)
            {
                if (bitmap != null)
                    bitmap.Dispose();
                bitmap = new Bitmap(picture);
            }
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            BitmapImage bitmapimage = new BitmapImage();

            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                    memory.Position = 0;

                    bitmapimage.BeginInit();
                    bitmapimage.StreamSource = memory;
                    bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapimage.EndInit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }

            return bitmapimage;
        }

        public void DisplayContour(PXCMPointI32[] contour, int blobNumber)
        {
            if (bitmap == null) return;
            lock (this)
            {
                Graphics g = Graphics.FromImage(bitmap);
                using (System.Drawing.Pen contourColor = new System.Drawing.Pen(System.Drawing.Color.Blue, 3.0f))
                {
                    for (int i = 0; i < contour.Length; i++)
                    {
                        int baseX = (int)contour[i].x;
                        int baseY = (int)contour[i].y;

                        if (i + 1 < contour.Length)
                        {
                            int x = (int)contour[i + 1].x;
                            int y = (int)contour[i + 1].y;

                            g.DrawLine(contourColor, new System.Drawing.Point(baseX, baseY), new System.Drawing.Point(x, y));
                        }
                        else
                        {
                            int x = (int)contour[0].x;
                            int y = (int)contour[0].y;
                            g.DrawLine(contourColor, new System.Drawing.Point(baseX, baseY), new System.Drawing.Point(x, y));
                        }
                    }
                }
                g.Dispose();
            }
        }

        public void DisplayJoints(PXCMHandData.JointData[][] nodes, int numOfHands)
        {
            if (bitmap == null) return;
            if (nodes == null) return;

            lock (this)
            {
                int scaleFactor = 1;

                Graphics g = Graphics.FromImage(bitmap);

                int centerX = 0;
                int centerY = 0;

                using (System.Drawing.Pen boneColor = new System.Drawing.Pen(System.Drawing.Color.DodgerBlue, 3.0f))
                {
                    for (int i = 0; i < numOfHands; i++)
                    {
                        if (nodes[i][0] == null) continue;
                        int baseX = (int)nodes[i][0].positionImage.x / scaleFactor;
                        int baseY = (int)nodes[i][0].positionImage.y / scaleFactor;

                        int wristX = (int)nodes[i][0].positionImage.x / scaleFactor;
                        int wristY = (int)nodes[i][0].positionImage.y / scaleFactor;

                        for (int j = 1; j < 22; j++)
                        {
                            if (nodes[i][j] == null) continue;
                            int x = (int)nodes[i][j].positionImage.x / scaleFactor;
                            int y = (int)nodes[i][j].positionImage.y / scaleFactor;

                            if (nodes[i][j].confidence <= 0) continue;

                            if (j == 2 || j == 6 || j == 10 || j == 14 || j == 18)
                            {

                                baseX = wristX;
                                baseY = wristY;
                            }

                            g.DrawLine(boneColor, new System.Drawing.Point(baseX, baseY), new System.Drawing.Point(x, y));
                            baseX = x;
                            baseY = y;
                        }

                        using (
                            System.Drawing.Pen red = new System.Drawing.Pen(System.Drawing.Color.Red, 3.0f),
                                black = new System.Drawing.Pen(System.Drawing.Color.Black, 3.0f),
                                green = new System.Drawing.Pen(System.Drawing.Color.Green, 3.0f),
                                blue = new System.Drawing.Pen(System.Drawing.Color.Blue, 3.0f),
                                cyan = new System.Drawing.Pen(System.Drawing.Color.Cyan, 3.0f),
                                yellow = new System.Drawing.Pen(System.Drawing.Color.Yellow, 3.0f),
                                orange = new System.Drawing.Pen(System.Drawing.Color.Orange, 3.0f))
                        {
                            System.Drawing.Pen currnetPen = black;

                            for (int j = 0; j < PXCMHandData.NUMBER_OF_JOINTS; j++)
                            {
                                float sz = 4;
                                //if (Labelmap.Checked)
                                if (true)
                                    sz = 2;

                                int x = (int)nodes[i][j].positionImage.x / scaleFactor;
                                int y = (int)nodes[i][j].positionImage.y / scaleFactor;

                                if (nodes[i][j].confidence <= 0) continue;

                                //Wrist
                                if (j == 0)
                                {
                                    currnetPen = black;
                                }

                                //Center
                                if (j == 1)
                                {
                                    currnetPen = red;
                                    sz += 4;

                                    centerX = x;
                                    centerY = y;

                                    int realX = 640 - x;

                                    if(realX > 640)
                                    {
                                        realX = 640;
                                    }
                                    else if(realX < 0)
                                    {
                                        realX = 0;
                                    }

                                    if (numOfHands == 2)
                                    {
                                        if(i == 1)
                                        {
                                            HandPoint.X = realX;
                                            HandPoint.Y = y;
                                        }
                                    }
                                    else
                                    {
                                        HandPoint.X = realX;
                                        HandPoint.Y = y;
                                    }
                                }

                                //Thumb
                                if (j == 2 || j == 3 || j == 4 || j == 5)
                                {
                                    currnetPen = green;
                                }
                                //Index Finger
                                if (j == 6 || j == 7 || j == 8 || j == 9)
                                {
                                    currnetPen = blue;
                                }
                                //Finger
                                if (j == 10 || j == 11 || j == 12 || j == 13)
                                {
                                    currnetPen = yellow;
                                }
                                //Ring Finger
                                if (j == 14 || j == 15 || j == 16 || j == 17)
                                {
                                    currnetPen = cyan;
                                }
                                //Pinkey
                                if (j == 18 || j == 19 || j == 20 || j == 21)
                                {
                                    currnetPen = orange;
                                }

                                if (j == 5 || j == 9 || j == 13 || j == 17 || j == 21)
                                {
                                    sz += 4;
                                }

                                g.DrawEllipse(currnetPen, x - sz / 2, y - sz / 2, sz, sz);
                            }
                        }
                    }

                }

                int yy = (int)HandPoint.Y;
                int xx = (int)HandPoint.X;
                if (yy >= 480)
                    yy = 479;

                int cnt = yy * 640 + centerX;

                ushort depth = DepthMap[cnt];

                if (depth == 0)
                {
                    return;
                }

                int delta = 0;

                if (depth != 0 && oldDepth != 0)
                {
                    delta = oldDepth - depth;
                    oldDepth = depth;
                }
                else if (depth != 0)
                {
                    oldDepth = depth;
                }

                if (gr.rightHandGesture == HandsRecognition.enumGesture.fist && 
                    gr.leftHandGesture == HandsRecognition.enumGesture.fist && 
                    numOfHands == 2)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        
                       
                        //if (Math.Abs(delta) < 100)
                        {
                            _trackball.Zoom(delta, 0.1);
                        }
                    });
                }
                else if (gr.rightHandGesture == HandsRecognition.enumGesture.fist)
                {
                    CaptureBorder.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                        MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Left);

                        if (oldHandPoint == HandPoint)
                        {
                            return;
                        }

                        oldHandPoint = HandPoint;

                        _trackball.OnMouseMovePublic(null, mouseEvent, HandPoint);
                    });
                }

                tbHandDepth.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    tbHandDepth.Text = depth.ToString();
                });

                g.Dispose();
            }
        }
        
        public void DisplayCursor(int numOfHands, Queue<PXCMPoint3DF32>[] cursorPoints, Queue<PXCMPoint3DF32>[] adaptivePoints, int[] cursorClick, PXCMCursorData.BodySideType[] handSideType)
        {
            

            if (bitmap == null) return;

            int scaleFactor = 1;
            Graphics g = Graphics.FromImage(bitmap);

            if (IsCursor)
            {
                System.Drawing.Color color = System.Drawing.Color.GreenYellow;
                System.Drawing.Pen pen = new System.Drawing.Pen(color, pSize);

                for (int i = 0; i < numOfHands; ++i)
                {
                    float sz = 8;
                    int blueColor = (handSideType[i] == PXCMCursorData.BodySideType.BODY_SIDE_LEFT)
                        ? 200
                        : (handSideType[i] == PXCMCursorData.BodySideType.BODY_SIDE_RIGHT) ? 100 : 0;

                    /// draw cursor trail
                    if (IsCursor)
                    {
                        if (IsAdaptive)
                        {
                            for (int j = 0; j < adaptivePoints[i].Count; j++)
                            {
                                float greenPart = (float)((Math.Max(Math.Min(adaptivePoints[i].ElementAt(j).z / scaleFactor, 0.7), 0.2) - 0.2) / 0.5);

                                pen.Color = System.Drawing.Color.FromArgb(255, (int)(255 * (1 - greenPart)), (int)(255 * greenPart), blueColor);
                                pen.Width = pSize;
                                int x = (int)adaptivePoints[i].ElementAt(j).x / scaleFactor;
                                int y = (int)adaptivePoints[i].ElementAt(j).y / scaleFactor;
                                g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);

                                adaptiveHandPoint.X = x;
                                adaptiveHandPoint.Y = y;

                                if (gr.CursorGesture == HandsRecognition.enumCursorGesture.HAND_OPENING)
                                {
                                    CaptureBorder.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                                    {
                                        MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                                        MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Left);

                                        if (oldAdaptiveHandPoint == adaptiveHandPoint)
                                        {
                                            return;
                                        }

                                        oldAdaptiveHandPoint = adaptiveHandPoint;

                                        _trackball.OnMouseMovePublic(null, mouseEvent, adaptiveHandPoint);
                                    });

                                }
                                else if (gr.CursorGesture == HandsRecognition.enumCursorGesture.HAND_CLOSING)
                                {
                                    ushort depth = 0;

                                    if (IsAdaptive)
                                    {
                                        depth = DepthMap[((int)adaptiveHandPoint.Y) * 640 + ((int)adaptiveHandPoint.X)];
                                    }
                                    else
                                    {
                                        depth = DepthMap[((int)cursorHandPoint.Y) * 640 + ((int)cursorHandPoint.X)];
                                    }

                                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                                    {
                                        int delta = 0;

                                        if (depth != 0 && oldDepth != 0)
                                        {
                                            delta = oldDepth - depth;
                                            oldDepth = depth;
                                        }
                                        else if (depth != 0)
                                        {
                                            oldDepth = depth;
                                        }

                                        //if (Math.Abs(delta) < 100)
                                        {
                                            _trackball.Zoom(delta, 0.1);
                                        }
                                    });

                                    tbHandDepth.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                                    {
                                        tbHandDepth.Text = depth.ToString();
                                    });

                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < cursorPoints[i].Count; j++)
                            {
                                float greenPart = (float)((Math.Max(Math.Min(cursorPoints[i].ElementAt(j).z / scaleFactor, 0.7), 0.2) - 0.2) / 0.5);

                                pen.Color = System.Drawing.Color.FromArgb(255, (int)(255 * (1 - greenPart)), (int)(255 * greenPart), blueColor);
                                pen.Width = pSize;
                                int x = (int)cursorPoints[i].ElementAt(j).x / scaleFactor;
                                int y = (int)cursorPoints[i].ElementAt(j).y / scaleFactor;
                                g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);

                                int realX = 640 - x;

                                if(realX > 640)
                                {
                                    realX = 640;
                                }
                                else if(realX < 0)
                                {
                                    realX = 0;
                                }

                                cursorHandPoint.X = realX;
                                cursorHandPoint.Y = y;

                                if (gr.CursorGesture == HandsRecognition.enumCursorGesture.HAND_CLOSING)
                                {
                                    CaptureBorder.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                                    {
                                        MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                                        MouseButtonEventArgs mouseEvent = new MouseButtonEventArgs(mouseDev, 0, MouseButton.Left);

                                        if (oldCursorHandPoint == cursorHandPoint)
                                        {
                                            return;
                                        }

                                        oldCursorHandPoint = cursorHandPoint;

                                        _trackball.OnMouseMovePublic(null, mouseEvent, cursorHandPoint);
                                    });
                                }
                                else if (gr.CursorGesture == HandsRecognition.enumCursorGesture.HAND_OPENING)
                                {
                                    
                                }
                            }
                        }
                    }

                    if (0 < cursorClick[i] && (IsCursor || IsAdaptive))
                    {
                        color = System.Drawing.Color.LightBlue;
                        pen = new System.Drawing.Pen(color, 10.0f);
                        sz = 32;

                        int x = 0, y = 0;
                        if (IsCursor && cursorPoints[i].Count() > 0)
                        {
                            x = (int)cursorPoints[i].ElementAt(cursorPoints[i].Count - 1).x / scaleFactor;
                            y = (int)cursorPoints[i].ElementAt(cursorPoints[i].Count - 1).y / scaleFactor;
                        }
                        else if (IsAdaptive && adaptivePoints[i].Count() > 0)
                        {
                            x = (int)adaptivePoints[i].ElementAt(adaptivePoints[i].Count - 1).x / scaleFactor;
                            y = (int)adaptivePoints[i].ElementAt(adaptivePoints[i].Count - 1).y / scaleFactor;
                        }
                        g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);
                    }
                }
                pen.Dispose();
            }
        }

        public void DisplayFinal()
        {
            try
            {
                if (bitmap != null)
                {
#if true
                    Bitmap tmp = bitmap.Clone() as Bitmap;

                    bitmap.Dispose();

                    tmp.RotateFlip(RotateFlipType.RotateNoneFlipX);

                    image1.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        BitmapImage imageSource = BitmapToImageSource(tmp);

                        tmp.Dispose();

                        image1.Source = imageSource;
                    });
#else
                    bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);

                    image1.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        image1.Source = BitmapToImageSource(bitmap);
                    });
#endif
                }
            }
            catch (Exception ex)
            {
                string err = ex.Message + "\r\n" + ex.StackTrace;
            }
        }

        public PXCMCapture.DeviceInfo GetCheckedDeviceInfo()
        {
            return Devices.ElementAt(0).Value;
        }

        private void chkLabel_Checked(object sender, RoutedEventArgs e)
        {
            if (gr != null)
            {
                gr.IsLabeled = true;
            }
        }

        private void chkLabel_Unchecked(object sender, RoutedEventArgs e)
        {
            if (gr != null)
            {
                gr.IsLabeled = false;
            }
        }
    }
}
