using System.Collections.Generic;
using ExileCore.Shared;
using System;
using SharpDX;
using ExileCore;
using System.Windows.Forms;

namespace TujenMem;

public class RestartableTask
{
  public SyncTask<bool> Task { get; private set; }
  public string Name { get; }
  public Func<SyncTask<bool>> TaskFactory { get; }
  public int MaxRetries { get; }
  public int CurrentRetries { get; private set; }
  public bool IsCompleted { get; private set; }
  public bool IsInterrupted { get; private set; }
  public DateTime LastMouseMove { get; private set; }
  public Vector2 LastMousePosition { get; private set; }
  public Action OnInterrupt { get; }
  private bool _isPaused = false;
  private SyncTask<bool> _stuckDetectionTask;
  public ref SyncTask<bool> StuckDetectionTask => ref _stuckDetectionTask;

  public RestartableTask(Func<SyncTask<bool>> taskFactory, string name, Action onInterrupt, int maxRetries = 3)
  {
    TaskFactory = taskFactory;
    Task = taskFactory();
    Name = name;
    MaxRetries = maxRetries;
    CurrentRetries = 0;
    LastMouseMove = DateTime.Now;
    LastMousePosition = Input.MousePosition;
    OnInterrupt = onInterrupt;
    _stuckDetectionTask = StuckDetection();
  }

  private async SyncTask<bool> StuckDetection()
  {
    while (!IsCompleted && !IsInterrupted)
    {
      if (!_isPaused)
      {
        var currentPos = Input.MousePosition;
        if (!currentPos.Equals(LastMousePosition))
        {
          LastMouseMove = DateTime.Now;
          LastMousePosition = currentPos;
        }
        else if ((DateTime.Now - LastMouseMove).TotalSeconds > 5)
        {
          Log.Error($"Task {Name} appears to be stuck. Attempting restart...");
          if (CanRetry())
          {
            MarkInterrupted();
            IncrementRetries();
            Reset();
            Task = TaskFactory();
            Log.Debug($"Restarted task {Name} (attempt {CurrentRetries}/{MaxRetries})");
          }
          else
          {
            Log.Error($"Task {Name} exceeded maximum retry attempts. Stopping.");
            MarkInterrupted();
            return false;
          }
        }
      }
      await InputAsync.Wait();
    }
    return true;
  }

  public void PauseStuckDetection()
  {
    _isPaused = true;
    Log.Debug($"Paused stuck detection for task: {Name}");
  }

  public void ResumeStuckDetection()
  {
    _isPaused = false;
    LastMouseMove = DateTime.Now; // Reset the timer when resuming
    Log.Debug($"Resumed stuck detection for task: {Name}");
  }

  public void MarkCompleted()
  {
    IsCompleted = true;
  }

  public void MarkInterrupted()
  {
    IsInterrupted = true;
    OnInterrupt?.Invoke();
  }

  public bool CanRetry()
  {
    return !IsCompleted && !IsInterrupted && CurrentRetries < MaxRetries;
  }

  public void IncrementRetries()
  {
    CurrentRetries++;
  }

  public void Reset()
  {
    Task = TaskFactory();
    LastMouseMove = DateTime.Now;
    LastMousePosition = Input.MousePosition;
  }
}

public class Scheduler
{
  public static TujenMem Instance = TujenMem.Instance;
  public Queue<SyncTask<bool>> Tasks = new();
  public Queue<RestartableTask> RestartableTasks = new();
  public SyncTask<bool> CurrentTask = null;
  public RestartableTask CurrentRestartableTask = null;

  public Scheduler(params SyncTask<bool>[] tasks)
  {
    foreach (var task in tasks)
    {
      Tasks.Enqueue(task);
    }
  }

  public void AddTask(SyncTask<bool> task, string name = null)
  {
    if (name != null)
    {
      Log.Debug("Adding task: " + name);
    }
    else if (task != null)
    {
      Log.Debug("Adding task: " + task.ToString());
    }
    Tasks.Enqueue(task);
  }

  public void AddRestartableTask(Func<SyncTask<bool>> taskFactory, string name, Action onInterrupt, int maxRetries = 3)
  {
    Log.Debug($"Adding restartable task: {name} (max retries: {maxRetries})");
    RestartableTasks.Enqueue(new RestartableTask(taskFactory, name, onInterrupt, maxRetries));
  }

  public void AddTasks(params SyncTask<bool>[] tasks)
  {
    foreach (var task in tasks)
    {
      Tasks.Enqueue(task);
    }
  }

  public void Run()
  {
    if (CurrentTask == null && CurrentRestartableTask == null)
    {
      if (RestartableTasks.Count > 0)
      {
        CurrentRestartableTask = RestartableTasks.Dequeue();
        CurrentTask = CurrentRestartableTask.Task;
        Log.Debug($"Starting restartable task: {CurrentRestartableTask.Name}");
        // Start the stuck detection task
        TaskUtils.RunOrRestart(ref CurrentRestartableTask.StuckDetectionTask, () => null);
      }
      else if (Tasks.Count > 0)
      {
        CurrentTask = Tasks.Dequeue();
      }
    }

    if (CurrentRestartableTask != null)
    {
      CurrentTask.GetAwaiter().OnCompleted(() =>
      {
        if (CurrentRestartableTask != null)
        {
          CurrentRestartableTask.MarkCompleted();
          Log.Debug($"Completed restartable task: {CurrentRestartableTask.Name}");
          CurrentRestartableTask = null;
        }
        CurrentTask = null;
      });
    }
    else if (CurrentTask != null)
    {
      CurrentTask.GetAwaiter().OnCompleted(() =>
      {
        CurrentTask = null;
      });
    }

    if (CurrentTask != null)
    {
      InputAsync.LOCK_CONTROLLER = true;
      TaskUtils.RunOrRestart(ref CurrentTask, () => null);
    }
  }

  public void Stop()
  {
    if (CurrentRestartableTask != null)
    {
      CurrentRestartableTask.MarkInterrupted();
      Log.Debug($"Interrupted restartable task: {CurrentRestartableTask.Name}");
    }
    CurrentRestartableTask = null;
    CurrentTask = null;
  }

  public void Clear()
  {
    Tasks.Clear();
    RestartableTasks.Clear();
  }
}