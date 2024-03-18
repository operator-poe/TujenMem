
using System.Collections.Generic;
using ExileCore.Shared;

namespace TujenMem;

public class Scheduler
{
  public static TujenMem Instance = TujenMem.Instance;
  public Queue<SyncTask<bool>> Tasks = new();
  public SyncTask<bool> CurrentTask = null;

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

  public void AddTasks(params SyncTask<bool>[] tasks)
  {
    foreach (var task in tasks)
    {
      Tasks.Enqueue(task);
    }
  }

  public void Run()
  {
    if (CurrentTask == null && Tasks.Count > 0)
    {
      CurrentTask = Tasks.Dequeue();
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
    CurrentTask = null;
  }

  public void Clear()
  {
    Tasks.Clear();
  }
}