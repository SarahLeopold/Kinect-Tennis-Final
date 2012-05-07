// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using ShapeGame.Utils;
using ShapeGame;
using ShapeGame.Speech;
using System.Drawing;


namespace Kinect_Tennis_Final
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

        }

        bool closing = false;
        const int skeletonCount = 6;
        Skeleton[] allSkeletons = new Skeleton[skeletonCount];
        private SpeechRecognizer mySpeechRecognizer;

        //private Skeleton[] allSkeletons;
        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        private Rect playerBounds;

        bool hitable; //defines when ball CAN be hit
        bool hit; //defines when ball HAS been hit
        int p1score = 0;  //score++ if hit=false, also resets ball.
        int p2score = 0;
        bool firsthit;
        bool serve; //false before serve, true after serve
        bool p1righty; //true if right handed
        bool p2righty;
        bool gameover;
        Random rdm = new Random();
        RotateTransform rotatetransform1 = new RotateTransform(270);
        RotateTransform rotatetransform2 = new RotateTransform(35);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);
            hitable = false;
            hit = false;
            serve = false;
            p1righty=true;
            p2righty=true;
            firsthit = false;
            gameover = false;
            ellipse1.Width = 45;
            ellipse1.Height = 45;
            ellipse1.Opacity = 0;
            label1.Content = 0;
            label2.Content = 0;

            playerBounds.X = 0;
            //may need to change these
            this.playerBounds.Width = this.canvas1.ActualWidth * .7;
            this.playerBounds.Y = this.canvas1.ActualHeight * .05;
            this.playerBounds.Height = this.canvas1.ActualHeight;

        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor old = (KinectSensor)e.OldValue;

            StopKinect(old);

            KinectSensor sensor = (KinectSensor)e.NewValue;

            if (sensor == null)
            {
                return;
            }




            var parameters = new TransformSmoothParameters
            {
                Smoothing = 0.3f,
                Correction = 0.0f,
                Prediction = 0.0f,
                JitterRadius = 1.0f,
                MaxDeviationRadius = 0.5f
            };
            sensor.SkeletonStream.Enable(parameters);

            sensor.SkeletonStream.Enable();

            sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(sensor_AllFramesReady);
            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

            try
            {
                sensor.Start();
            }
            catch (System.IO.IOException)
            {
                kinectSensorChooser1.AppConflictOccurred();
            }

            this.mySpeechRecognizer = SpeechRecognizer.Create();
            this.mySpeechRecognizer.SaidSomething += this.RecognizerSaidSomething;
            this.mySpeechRecognizer.Start(sensor.AudioSource);

        }

        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (closing)
            {
                return;
            }
            

            canvas1.Children.Clear();
            canvas2.Children.Clear();
            foreach (var player in this.players)
            {
                if (player.Key == 0)
                {
                    player.Value.Draw(canvas1.Children);//if player 1
                }
                else
                {
                    player.Value.Draw(canvas2.Children);//if player 2
                }
            }

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    int skeletonSlot = 0;

                    if ((this.allSkeletons == null) || (this.allSkeletons.Length != skeletonFrame.SkeletonArrayLength))
                    {
                        this.allSkeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    }

                    skeletonFrame.CopySkeletonDataTo(this.allSkeletons);

                    foreach (Skeleton skeleton in this.allSkeletons)
                    {
                        if (SkeletonTrackingState.Tracked == skeleton.TrackingState)
                        {
                            Player player;
                            if (this.players.ContainsKey(skeletonSlot))
                            {
                                player = this.players[skeletonSlot];
                            }
                            else
                            {
                                player = new Player(skeletonSlot); //skeletonslot 0 is player1 skeletonslot 1 is player 2
                                player.SetBounds(this.playerBounds); //this would be where we change player bounds - will this work?
                                this.players.Add(skeletonSlot, player);
                            }

                            player.LastUpdated = DateTime.Now;

                            // Update player's bone and joint positions
                            if (skeleton.Joints.Count > 0)
                            {
                                player.IsAlive = true;
                                // Head, hands, feet (hit testing happens in order here)
                                player.UpdateJointPosition(skeleton.Joints, JointType.Head);
                                player.UpdateJointPosition(skeleton.Joints, JointType.HandLeft);
                                player.UpdateJointPosition(skeleton.Joints, JointType.HandRight);
                                player.UpdateJointPosition(skeleton.Joints, JointType.FootLeft);
                                player.UpdateJointPosition(skeleton.Joints, JointType.FootRight);

                                // Hands and arms
                                player.UpdateBonePosition(skeleton.Joints, JointType.HandRight, JointType.WristRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.WristRight, JointType.ElbowRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ElbowRight, JointType.ShoulderRight);

                                player.UpdateBonePosition(skeleton.Joints, JointType.HandLeft, JointType.WristLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.WristLeft, JointType.ElbowLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ElbowLeft, JointType.ShoulderLeft);

                                // Head and Shoulders
                                player.UpdateBonePosition(skeleton.Joints, JointType.ShoulderCenter, JointType.Head);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ShoulderLeft, JointType.ShoulderCenter);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ShoulderCenter, JointType.ShoulderRight);

                                // Legs
                                player.UpdateBonePosition(skeleton.Joints, JointType.HipLeft, JointType.KneeLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.KneeLeft, JointType.AnkleLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.AnkleLeft, JointType.FootLeft);

                                player.UpdateBonePosition(skeleton.Joints, JointType.HipRight, JointType.KneeRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.KneeRight, JointType.AnkleRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.AnkleRight, JointType.FootRight);

                                player.UpdateBonePosition(skeleton.Joints, JointType.HipLeft, JointType.HipCenter);
                                player.UpdateBonePosition(skeleton.Joints, JointType.HipCenter, JointType.HipRight);

                                // Spine
                                player.UpdateBonePosition(skeleton.Joints, JointType.HipCenter, JointType.ShoulderCenter);

                                GetCameraPoint(skeleton, e, skeletonSlot);

                                if (skeletonSlot == 0 && p1righty == true)
                                {
                                    ScalePosition(p1racket, skeleton.Joints[JointType.HandRight], skeletonSlot);
                                }
                                else if (skeletonSlot == 0 && p1righty == false)
                                {
                                    ScalePosition(p1racket, skeleton.Joints[JointType.HandLeft], skeletonSlot);
                                }
                                else if (skeletonSlot != 0 && p2righty == false)
                                {
                                    ScalePosition(p2racket, skeleton.Joints[JointType.HandLeft], skeletonSlot);
                                }
                                else
                                {
                                    ScalePosition(p2racket, skeleton.Joints[JointType.HandRight], skeletonSlot);
                                }


                            }
                        }

                        skeletonSlot++;


                    }
                }

            }



            if (serve==true && firsthit == true && gameover==false)
            {
                checkForHit();
                if (hit == true)
                {
                    firsthit=false;
                }
            }


            if (serve == true && firsthit==false && gameover==false)
            {
                if (ellipse1.Width > 40 && ellipse1.Height > 40 && hit==false)  //determines when the ball can be hit
                {
                    hitable = true;
                }
                else
                {
                    hitable = false;
                }

                //checks for hit when hitable
                if (hitable == true && hit == false)
                {
                    checkForHit();
                }

                if (hit==true)
                {
                    ellipse1.Width--;
                    ellipse1.Height--;
                    hitable=false;
                }
                
                if (ellipse1.Width <= 5 && ellipse1.Height <= 5)  //once the ball is at min, start getting bigger
                {

                    if (Canvas.GetLeft(ellipse1) < (MainCanvas.Width/2))
                    {
                        int x = rdm.Next(405, 760);
                        //int y = rdm.Next((int)((MainCanvas.Height/2)+50);
                        Canvas.SetLeft(ellipse1, x);
                        //Canvas.SetTop(ellipse1, y);
                    }

                    else
                    {
                        int x = rdm.Next((int)((MainCanvas.Width / 2)-50));
                        //int y = rdm.Next((int)((MainCanvas.Height/2)+50);
                        Canvas.SetLeft(ellipse1, x);
                        //Canvas.SetTop(ellipse1, y);
                    }

                    hit = false;
                }

                if(ellipse1.Width<50 && ellipse1.Height<50 && hit==false)
                {
                    ellipse1.Width++;
                    ellipse1.Height++;
                }


                if (ellipse1.Width >= 50 && ellipse1.Height >= 50 && hit == false)
                {
                    //player missed ball
                    ellipse1.Opacity = 0;
                    serve = false;
                    //increment score label

                    if (Canvas.GetLeft(ellipse1)>=(MainCanvas.Width/2))
                    {//do this by ball position in relation to canvas instead?
                        p1score += 15;
                        label1.Content = p1score;
                        Canvas.SetLeft(ellipse1, 100.0);
                        //reset position to canvas1 and max size 
                    }
                    if (Canvas.GetLeft(ellipse1) < (MainCanvas.Width / 2))
                    {
                        p2score += 15;
                        label2.Content = p2score;
                        Canvas.SetLeft(ellipse1, 505.0);
                        //reset position to canvas2 and max size
                    }
                }
            }

            if (p1score >= 60 || p2score >= 60)
            {
                gameover = true;
                label1.Content="Game Over: " + p1score;
                label2.Content = "Game Over: " + p2score;
                ellipse1.Opacity = 0;
                p1racket.Opacity = 0;
                p2racket.Opacity = 0;
            }

        }

        void checkForHit() //this is where to adjust ball angle based on hit
        {

                //ball
                double ellipseCenterX = Canvas.GetLeft(ellipse1) + ellipse1.Width / 2;
                double ellipseCenterY = Canvas.GetTop(ellipse1) + ellipse1.Height / 2;

                double p1rightCenterX = Canvas.GetLeft(p1racket) + p1racket.Width / 2;
                double p1rightCenterY = Canvas.GetTop(p1racket) + p1racket.Height / 2;
                
                double p2rightCenterX = Canvas.GetLeft(p2racket) + p2racket.Width / 2;
                double p2rightCenterY = Canvas.GetTop(p2racket) + p2racket.Height / 2;

                double p1upperBoundRight = ellipse1.Width / 2 + p1racket.Width / 2;
                double p1actualDistanceRight = Math.Sqrt(Math.Pow(ellipseCenterX - p1rightCenterX, 2) + Math.Pow(ellipseCenterY - p1rightCenterY, 2));

                double p2upperBoundRight = ellipse1.Width / 2 + p2racket.Width / 2;
                double p2actualDistanceRight = Math.Sqrt(Math.Pow(ellipseCenterX - p2rightCenterX, 2) + Math.Pow(ellipseCenterY - p2rightCenterY, 2));



                if (p1actualDistanceRight < p1upperBoundRight || p2actualDistanceRight < p2upperBoundRight)
                {
                    //set hit to true
                    hit = true;
                }
        }

        void GetCameraPoint(Skeleton skeleton, AllFramesReadyEventArgs e, int slot)
        {
            
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (depth == null ||
                    kinectSensorChooser1.Kinect == null)
                {
                    return;
                }
                
                DepthImagePoint rightDepthPoint =
                    depth.MapFromSkeletonPoint(skeleton.Joints[JointType.HandRight].Position);

                ColorImagePoint rightColorPoint =
                    depth.MapToColorImagePoint(rightDepthPoint.X, rightDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);
                
                DepthImagePoint leftDepthPoint =
                    depth.MapFromSkeletonPoint(skeleton.Joints[JointType.HandLeft].Position);

                ColorImagePoint leftColorPoint =
                    depth.MapToColorImagePoint(leftDepthPoint.X, leftDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);

                DepthImagePoint shoulderLeftDepthPoint =
                    depth.MapFromSkeletonPoint(skeleton.Joints[JointType.ShoulderLeft].Position);

                ColorImagePoint shoulderLeftColorPoint =
                    depth.MapToColorImagePoint(shoulderLeftDepthPoint.X, shoulderLeftDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);

                DepthImagePoint shoulderRightDepthPoint =
                    depth.MapFromSkeletonPoint(skeleton.Joints[JointType.ShoulderLeft].Position);

                ColorImagePoint shoulderRightColorPoint =
                    depth.MapToColorImagePoint(shoulderRightDepthPoint.X, shoulderRightDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);

                

                if (slot == 0 && p1righty==true)
                {
                    CameraPosition(p1racket, rightColorPoint);
                }
                else if (slot == 0 && p1righty == false)
                {
                    CameraPosition(p1racket, leftColorPoint);
                }
                else if (slot != 0 && p2righty == true)
                {
                    CameraPosition(p2racket, rightColorPoint);
                }
                else
                {
                    CameraPosition(p2racket, leftColorPoint);
                }
            }
        }


        private void CameraPosition(FrameworkElement element, ColorImagePoint point) //this is where we can adjust rackets
        {
            //try two background canvas, pass slot
            //Divide by 2 for width and height so point is right in the middle 
            // instead of in top/left corner
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 2);
            
        }

        private void ScalePosition(FrameworkElement element, Joint joint, int slot)
        {
            //convert the value to X/Y
            //Joint scaledJoint = joint.ScaleTo(1280, 720); 

            //convert & scale (.3 = means 1/3 of joint distance)
            Joint scaledJoint = joint.ScaleTo(380, 301, .9f, .5f);

            if (slot == 0)
            {
                Canvas.SetLeft(element, scaledJoint.Position.X);
                Canvas.SetTop(element, scaledJoint.Position.Y);
            }
            else
            {
                Canvas.SetLeft(element, scaledJoint.Position.X+380);
                Canvas.SetTop(element, scaledJoint.Position.Y);     
            }

        }

        private void RecognizerSaidSomething(object sender, SpeechRecognizer.SaidSomethingEventArgs e)
        {
            //should this be moved to allframesready?
            
            if (serve == false)
            {
                switch (e.Verb)
                {
                    case SpeechRecognizer.Verbs.Serve:
                        ellipse1.Height = 45;
                        ellipse1.Width = 45;
                        ellipse1.Opacity = 1;
                        firsthit = true;
                        serve = true;
                        break;
                    case SpeechRecognizer.Verbs.OneLeft:
                        if (p1righty == true)
                        {
                            p1racket.RenderTransform = rotatetransform1;
                        }
                        p1righty = false;
                        break;
                    case SpeechRecognizer.Verbs.OneRight:
                        if (p1righty == false)
                        {
                            p1racket.RenderTransform = rotatetransform2;
                        }
                        p1righty = true;
                        break;
                    case SpeechRecognizer.Verbs.TwoLeft:
                        if (p2righty == true)
                        {
                            p2racket.RenderTransform = rotatetransform1;
                        }
                        p2righty = false;
                        break;
                    case SpeechRecognizer.Verbs.TwoRight:
                        if (p2righty == false)
                        {
                            p2racket.RenderTransform = rotatetransform2;
                        }
                        p2righty = true;
                        break;
                }
            }
        }

        private void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if (sensor.IsRunning)
                {
                    //stop sensor 
                    sensor.Stop();

                    //stop audio if not null
                    if (sensor.AudioSource != null)
                    {
                        sensor.AudioSource.Stop();
                    }

                    if (this.mySpeechRecognizer != null)
                    {
                        this.mySpeechRecognizer.Stop();
                        this.mySpeechRecognizer.SaidSomething -= this.RecognizerSaidSomething;
                        this.mySpeechRecognizer.Dispose();
                        this.mySpeechRecognizer = null;
                    }


                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            StopKinect(kinectSensorChooser1.Kinect);
        }


    }
}
//things that still need to be done:
//racket rotate for backhand and forehand
