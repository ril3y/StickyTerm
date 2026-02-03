using System.IO;
using System.Security.Principal;
using StickyTerm.Models;
using Microsoft.Win32.TaskScheduler;

namespace StickyTerm.Services;

/// <summary>
/// Service for managing Windows startup behavior using Task Scheduler.
/// Uses Task Scheduler to run the app with elevated privileges at logon.
/// </summary>
public class StartupService : IStartupService
{
    private const string TaskName = "StickyTerm Auto-Start";
    private readonly ILoggingService _logger;

    public StartupService(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                using var ts = new TaskService();
                return ts.FindTask(TaskName) != null;
            }
            catch (Exception ex)
            {
                _logger.Log(LogEntry.Warning($"Failed to check startup status: {ex.Message}"));
                return false;
            }
        }
    }

    public bool EnableStartup()
    {
        // Must run as admin to create elevated task
        if (!IsRunningAsAdmin())
        {
            _logger.Log(LogEntry.Warning("Administrator privileges required to enable startup"));
            return false;
        }

        try
        {
            using var ts = new TaskService();

            // Remove existing task if present
            var existingTask = ts.FindTask(TaskName);
            if (existingTask != null)
            {
                ts.RootFolder.DeleteTask(TaskName, false);
            }

            // Create new task definition
            var td = ts.NewTask();
            td.RegistrationInfo.Description = "Starts StickyTerm COM Port Manager at Windows startup with elevated privileges for COM port tracking.";
            td.RegistrationInfo.Author = "StickyTerm";

            // Principal settings - run with highest privileges
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Principal.LogonType = TaskLogonType.InteractiveToken;

            // Trigger: At user logon
            td.Triggers.Add(new LogonTrigger
            {
                Delay = TimeSpan.FromSeconds(10) // Small delay to let system settle
            });

            // Settings
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.Zero; // Run indefinitely
            td.Settings.AllowDemandStart = true;
            td.Settings.Hidden = false;
            td.Settings.StartWhenAvailable = true;

            // Action: Start the application minimized
            var exePath = Environment.ProcessPath ??
                         Path.Combine(AppContext.BaseDirectory, "StickyTerm.exe");
            td.Actions.Add(new ExecAction(exePath, "--minimized", AppContext.BaseDirectory));

            // Register the task
            ts.RootFolder.RegisterTaskDefinition(TaskName, td);

            _logger.Log(LogEntry.Info("Startup task created successfully - StickyTerm will start with Windows"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to create startup task: {ex.Message}", ex.ToString()));
            return false;
        }
    }

    public bool DisableStartup()
    {
        try
        {
            using var ts = new TaskService();

            var task = ts.FindTask(TaskName);
            if (task != null)
            {
                ts.RootFolder.DeleteTask(TaskName);
                _logger.Log(LogEntry.Info("Startup task removed - StickyTerm will no longer start with Windows"));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to remove startup task: {ex.Message}", ex.ToString()));
            return false;
        }
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
