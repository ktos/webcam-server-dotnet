using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using System.Collections.ObjectModel;
using dshow;
using dshow.Core;
using System.Drawing;
using System.Timers;

namespace CamServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow This;
        public MainWindow()
        {
            InitializeComponent();

            This = this;
        }
        bool simulated = false;
        bool started = false;
        int fps = 1;
        WebServer server;
        CaptureDevice cd = new CaptureDevice();
        Timer simTim;
        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            started = !started;
            if (!simulated)
            {
                if (captureDevicesLst.SelectedIndex == -1)
                {
                    started = false;
                    return;
                }
            }

            if (started)
            {
                int portNo = Int32.Parse(portBox.Text);

                server = new WebServer(portNo);
                testBlock.Text = "http://localhost:" + portNo.ToString() + "/";



                Int32.TryParse(fpsBox.Text, out fps);
                int delay = 1000 / fps;
                startBtn.Content = "Stop streaming";
                if (simulated)
                {
                    List<byte[]> pics = new List<byte[]>();
                    foreach (var file in Directory.EnumerateFiles("SimulatedCamPics"))
                    {
                        if (file.ToLower().Contains(".jpg") || file.ToLower().Contains(".png"))
                        {
                            pics.Add(File.ReadAllBytes(file));
                        }
                    }

                    Log("simulated: readed " + pics.Count.ToString() + " image(s)");
                    simTim = new Timer(delay);
                    int i = 0;
                    simTim.Elapsed += (s, ev) =>
                    {
                        simTim.Stop();
                        DateTime start = DateTime.Now;
                        server.SendImg(pics[i % pics.Count]);
                        TimeSpan finish = DateTime.Now - start;
                        Log("simulated send finished: " + finish.TotalMilliseconds.ToString() + "ms");
                        i++;
                        if (i % pics.Count == 0)
                            i = 0;
                        simTim.Start();
                    };
                    simTim.Start();
                }
                else
                {
                    cd = new CaptureDevice();
                    cd.VideoSource = captureDevices[captureDevicesLst.SelectedIndex].MonikerString;
                    DateTime lastFrame = DateTime.Now;
                    bool doResize = (bool)doResizeCheck.IsChecked;
                    cd.NewFrame += (s, evt) =>
                    {
                        //send jpg to all client...
                        if ((DateTime.Now - lastFrame).TotalMilliseconds >= delay)
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                DateTime firstStart = DateTime.Now;
                                DateTime start = DateTime.Now;
                                Bitmap resized;
                                TimeSpan finish;
                                if (doResize)
                                {
                                    AForge.Imaging.Filters.Resize resizeFilter = new AForge.Imaging.Filters.Resize(1280, 1024);
                                    resized = resizeFilter.Apply(evt.Bitmap);
                                    finish = DateTime.Now - start;
                                    Log("resize finished: " + finish.TotalMilliseconds.ToString() + "ms");
                                }
                                else
                                    resized = evt.Bitmap;

                                start = DateTime.Now;
                                resized.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);

                                finish = DateTime.Now - start;
                                Log("saveToStream finished: " + finish.TotalMilliseconds.ToString() + "ms");

                                start = DateTime.Now;
                                server.SendImg(ms.ToArray());
                                finish = DateTime.Now - start;
                                Log("send finished: " + finish.TotalMilliseconds.ToString() + "ms");

                                finish = DateTime.Now - lastFrame;
                                lastFrame = DateTime.Now;
                                Log("cam refresh: " + finish.TotalMilliseconds.ToString() + "ms (FPS: " + cd.FramesReceived.ToString() + ")");

                                finish = DateTime.Now - firstStart;
                                delay = (int)(1000 / fps) - (int)finish.TotalMilliseconds;
                            }
                            GC.Collect();
                        }
                    };
                    cd.Start();
                }
            }
            else
            {
                startBtn.Content = "Start streaming";
                if(cd != null)
                    cd.Stop();
                if (simTim != null)
                    simTim.Stop();
                server.DisconnectAll();
                server = null;
                GC.Collect();
            }
        }

        public void Log(string p)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                logBox.Text += DateTime.Now.ToString("HH:mm:ss.ff") + " - " + p + "\r\n";
                logScroll.ScrollToEnd();
            }));
        }

        FilterCollection captureDevices;
        private void captureDevicesLst_Loaded(object sender, RoutedEventArgs e)
        {
            captureDevices = new FilterCollection(FilterCategory.VideoInputDevice);
            foreach (Filter filt in captureDevices)
            {
                captureDevicesLst.Items.Add(filt.Name);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (cd != null)
                cd.Stop();
            if (server != null)
                server.Dispose();
            GC.Collect();
        }

        private void modeChooser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            simulated = (modeChooser.SelectedItem as ComboBoxItem).Content.ToString().Contains("Simulated");
        }

        private void textBlock1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(()=> {
                System.Diagnostics.Process.Start(testBlock.Text);
            }));
        }
    }
}
