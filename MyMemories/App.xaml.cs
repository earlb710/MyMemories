    using Microsoft.UI.Xaml;
using MyMemories.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? m_window;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        
        // Setup global exception handlers
        SetupGlobalExceptionHandlers();
    }

    /// <summary>
    /// Sets up global exception handlers for unhandled exceptions.
    /// </summary>
    private void SetupGlobalExceptionHandlers()
    {
        // Handle unhandled exceptions on UI thread
        this.UnhandledException += OnUnhandledException;
        
        // Handle unobserved task exceptions from async operations
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        
        // Handle unhandled exceptions in non-UI threads
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        
        Debug.WriteLine("[App] Global exception handlers configured");
    }

    /// <summary>
    /// Handles unhandled exceptions from the UI thread.
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[App] Unhandled UI exception: {e.Exception.Message}");
        
        // Mark as handled to prevent crash
        e.Handled = true;
        
        // Let global handler process it
        GlobalExceptionHandler.Instance.HandleUnhandledException(e.Exception, "UI Thread");
        
        // If it's a critical exception, don't mark as handled - let app crash gracefully
        if (GlobalExceptionHandler.Instance.IsCriticalException(e.Exception))
        {
            e.Handled = false;
            Debug.WriteLine("[App] Critical exception detected - allowing application to terminate");
        }
    }

    /// <summary>
    /// Handles unobserved exceptions from async Task operations.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($"[App] Unobserved task exception: {e.Exception.Message}");
        
        // Mark as observed to prevent crash
        e.SetObserved();
        
        // Let global handler process it
        GlobalExceptionHandler.Instance.HandleUnobservedTaskException(e.Exception);
    }

    /// <summary>
    /// Handles unhandled exceptions from non-UI threads (last resort).
    /// </summary>
    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Debug.WriteLine($"[App] Domain unhandled exception: {exception.Message}");
            Debug.WriteLine($"[App] Is Terminating: {e.IsTerminating}");
            
            // Let global handler process it
            GlobalExceptionHandler.Instance.HandleUnhandledException(exception, "AppDomain");
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        m_window.Activate();
    }
}
