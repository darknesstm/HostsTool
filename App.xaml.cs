using System;
using System.Windows;
using Forms = System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.Threading;

namespace HostsTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private bool _allowClose = false;
    private System.Drawing.Icon? _generatedIcon;
    
    // Single instance support
    private static Mutex? _mutex;
    private EventWaitHandle? _eventWaitHandle;
    private Thread? _namedEventThread;
    private const string UniqueMutexName = "HostsTool_SingleInstance_Mutex_8F3E9D2A";
    private const string UniqueEventName = "HostsTool_ShowWindow_Event_8F3E9D2A";

    // relative path inside app folder for the icon file
    private const string IconFileName = "Resources\\hosts_icon.ico";

    protected override void OnStartup(StartupEventArgs e)
    {
        // Check for single instance
        bool createdNew;
        _mutex = new Mutex(true, UniqueMutexName, out createdNew);

        if (!createdNew)
        {
            // Another instance is already running, signal it to show its window
            try
            {
                var existingEvent = EventWaitHandle.OpenExisting(UniqueEventName);
                existingEvent.Set();
            }
            catch
            {
                // If we can't signal, just exit silently
            }
            
            // Exit this instance
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Create event to receive signals from new instances
        _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, UniqueEventName);
        
        // Start background thread to listen for show window signals
        _namedEventThread = new Thread(() =>
        {
            while (_eventWaitHandle != null)
            {
                try
                {
                    if (_eventWaitHandle.WaitOne())
                    {
                        // Signal received from another instance - show main window
                        Dispatcher.Invoke(() =>
                        {
                            if (_mainWindow != null)
                            {
                                if (!_mainWindow.IsVisible)
                                {
                                    _mainWindow.Show();
                                }
                                _mainWindow.Activate();
                                _mainWindow.WindowState = WindowState.Normal;
                            }
                        });
                    }
                }
                catch
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstanceEventListener"
        };
        _namedEventThread.Start();

        // Create the main window but don't show it yet until tray icon is ready
        _mainWindow = new MainWindow();

        // Intercept window closing to implement "close to tray"
        _mainWindow.Closing += (s, args) =>
        {
            if (!_allowClose)
            {
                args.Cancel = true;
                _mainWindow.Hide();
            }
        };

        // Initialize notify icon (system tray)
        _notifyIcon = new Forms.NotifyIcon();
        _notifyIcon.Text = "HostsTool";

        // Determine full path for icon file
        var appDir = AppContext.BaseDirectory;
        var iconFullPath = Path.Combine(appDir, IconFileName);
        var iconDir = Path.GetDirectoryName(iconFullPath) ?? appDir;
        try
        {
            if (!Directory.Exists(iconDir))
                Directory.CreateDirectory(iconDir);
        }
        catch { }

        // If icon file exists, load it; otherwise generate and save once
        try
        {
            if (File.Exists(iconFullPath))
            {
                using var fs = File.OpenRead(iconFullPath);
                _generatedIcon = new System.Drawing.Icon(fs);
            }
            else
            {
                _generatedIcon = CreateMultiResolutionIcon("H", new int[] { 16, 32, 48, 64, 128, 256 });
                if (_generatedIcon != null)
                {
                    // Save to disk for future runs
                    try
                    {
                        using var fsOut = File.OpenWrite(iconFullPath);
                        _generatedIcon.Save(fsOut);
                        fsOut.Flush();
                    }
                    catch { /* ignore disk write failures */ }
                }
            }

            if (_generatedIcon != null)
            {
                _notifyIcon.Icon = _generatedIcon;

                // Set WPF Window icon - extract 32x32 size for better display in taskbar and title bar
                try
                {
                    if (File.Exists(iconFullPath))
                    {
                        // Load the icon file and extract the 32x32 size explicitly
                        using var iconStream = File.OpenRead(iconFullPath);
                        var decoder = new IconBitmapDecoder(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        
                        // Find the best frame - prefer 32x32 or largest available
                        BitmapFrame? bestFrame = null;
                        int targetSize = 32;
                        int bestMatch = int.MaxValue;
                        
                        foreach (var frame in decoder.Frames)
                        {
                            int size = Math.Max(frame.PixelWidth, frame.PixelHeight);
                            int diff = Math.Abs(size - targetSize);
                            if (diff < bestMatch)
                            {
                                bestMatch = diff;
                                bestFrame = frame;
                            }
                        }
                        
                        if (bestFrame != null)
                        {
                            _mainWindow.Icon = bestFrame;
                        }
                        else
                        {
                            // Fallback to first frame
                            _mainWindow.Icon = decoder.Frames[0];
                        }
                    }
                    else
                    {
                        // fallback: convert icon to bitmap and set as ImageSource
                        using var bmp = _generatedIcon.ToBitmap();
                        using var ms = new MemoryStream();
                        bmp.Save(ms, ImageFormat.Png);
                        ms.Seek(0, SeekOrigin.Begin);
                        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        _mainWindow.Icon = decoder.Frames[0];
                    }
                }
                catch { /* ignore icon set failures */ }
            }
            else
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        _notifyIcon.Visible = true;

        // Context menu
        var contextMenu = new Forms.ContextMenuStrip();
        var showHideItem = new Forms.ToolStripMenuItem("Show/Hide");
        showHideItem.Click += (s, args) => ToggleMainWindowVisibility();
        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (s, args) => ShutdownApplication();

        contextMenu.Items.Add(showHideItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Double-click toggles visibility
        _notifyIcon.DoubleClick += (s, args) => ToggleMainWindowVisibility();

        // Show main window on start
        _mainWindow.Show();
        _mainWindow.Closed += (s, args) => { /* keep window instance until app exits */ };
    }

    private void ToggleMainWindowVisibility()
    {
        if (_mainWindow == null)
            return;

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void ShutdownApplication()
    {
        // Allow window to close and then shut down
        _allowClose = true;

        // Clean up notify icon
        if (_notifyIcon != null)
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch { }
            _notifyIcon = null;
        }

        // Dispose generated icon if any
        if (_generatedIcon != null)
        {
            try { _generatedIcon.Dispose(); } catch { }
            _generatedIcon = null;
        }

        // Close main window if open
        _mainWindow?.Close();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Clean up single instance resources
        if (_eventWaitHandle != null)
        {
            try
            {
                _eventWaitHandle.Set(); // Signal thread to exit
                _eventWaitHandle.Dispose();
            }
            catch { }
            _eventWaitHandle = null;
        }

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            catch { }
            _mutex = null;
        }

        if (_notifyIcon != null)
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch { }
            _notifyIcon = null;
        }

        if (_generatedIcon != null)
        {
            try { _generatedIcon.Dispose(); } catch { }
            _generatedIcon = null;
        }

        base.OnExit(e);
    }

    // Create a multi-resolution ICO with PNG-encoded images for the requested sizes
    private static System.Drawing.Icon? CreateMultiResolutionIcon(string text, int[] sizes)
    {
        if (sizes == null || sizes.Length == 0) return null;

        // Prepare PNG images for each size
        var pngImages = new System.Collections.Generic.List<byte[]>();
        foreach (var size in sizes)
        {
            using var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                // Enable high quality rendering
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                g.Clear(System.Drawing.Color.White);

                float fontSize = size * 0.65f; // Slightly smaller for better fit
                System.Drawing.Font font;
                try
                {
                    font = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                }
                catch
                {
                    font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                }

                var textSize = g.MeasureString(text, font, System.Drawing.Point.Empty, System.Drawing.StringFormat.GenericTypographic);
                float x = (size - textSize.Width) / 2f;
                float y = (size - textSize.Height) / 2f;

                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
                g.DrawString(text, font, brush, x, y, System.Drawing.StringFormat.GenericTypographic);
                
                font.Dispose();
            }

            using var ms = new MemoryStream();
            // Save as PNG to preserve alpha and good scaling inside ICO
            bmp.Save(ms, ImageFormat.Png);
            pngImages.Add(ms.ToArray());
        }

        // Build ICO file in memory where each image data is a PNG (modern Windows supports PNG in ICO)
        using var icoStream = new MemoryStream();
        using var bw = new BinaryWriter(icoStream);

        // ICONDIR
        bw.Write((short)0); // reserved
        bw.Write((short)1); // type = 1 for icons
        bw.Write((short)pngImages.Count); // count

        int imageDataOffset = 6 + (16 * pngImages.Count);
        int currentOffset = imageDataOffset;

        // Directory entries
        for (int i = 0; i < pngImages.Count; i++)
        {
            var png = pngImages[i];
            int size = sizes[i];
            bw.Write((byte)(size >= 256 ? 0 : size)); // width
            bw.Write((byte)(size >= 256 ? 0 : size)); // height
            bw.Write((byte)0); // color palette
            bw.Write((byte)0); // reserved
            bw.Write((short)1); // color planes
            bw.Write((short)32); // bits per pixel
            bw.Write(png.Length); // bytes in resource
            bw.Write(currentOffset); // offset
            currentOffset += png.Length;
        }

        // Image data
        for (int i = 0; i < pngImages.Count; i++)
        {
            bw.Write(pngImages[i]);
        }

        bw.Flush();
        icoStream.Seek(0, SeekOrigin.Begin);

        try
        {
            return new System.Drawing.Icon(icoStream);
        }
        catch
        {
            return null;
        }
    }
}
