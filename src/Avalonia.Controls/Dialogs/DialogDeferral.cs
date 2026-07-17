using System;
using System.Threading;
using Avalonia.Threading;

namespace Avalonia.Controls;

/// <summary>
/// Represents a deferred dialog operation.
/// </summary>
public sealed class DialogDeferral
{
    private Action? _complete;

    internal DialogDeferral(Action complete)
    {
        _complete = complete;
    }

    /// <summary>
    /// Signals that the deferred operation has completed.
    /// </summary>
    public void Complete()
    {
        Interlocked.Exchange(ref _complete, null)?.Invoke();
    }
}

internal sealed class DialogDeferralManager
{
    private readonly object _sync = new();
    private Action? _completed;
    private int _count = 1;
    private bool _acceptingDeferrals = true;
    private bool _completionScheduled;

    public DialogDeferralManager(Action completed)
    {
        _completed = completed;
    }

    public DialogDeferral GetDeferral()
    {
        lock (_sync)
        {
            if (!_acceptingDeferrals)
            {
                throw new InvalidOperationException(
                    "A dialog deferral must be requested before the event handler returns.");
            }

            _count++;
            return new DialogDeferral(CompleteOne);
        }
    }

    public void CompleteInitialDeferral()
    {
        var scheduleCompletion = false;
        lock (_sync)
        {
            if (!_acceptingDeferrals)
                return;

            _acceptingDeferrals = false;
            scheduleCompletion = CompleteOneLocked();
        }

        if (scheduleCompletion)
            ScheduleCompletion();
    }

    public void Cancel()
    {
        lock (_sync)
        {
            _acceptingDeferrals = false;
            _completed = null;
        }
    }

    private void CompleteOne()
    {
        var scheduleCompletion = false;
        lock (_sync)
        {
            scheduleCompletion = CompleteOneLocked();
        }

        if (scheduleCompletion)
            ScheduleCompletion();
    }

    private bool CompleteOneLocked()
    {
        if (_count == 0)
            return false;

        _count--;
        if (_count != 0 || _completionScheduled || _completed is null)
            return false;

        _completionScheduled = true;
        return true;
    }

    private void ScheduleCompletion()
    {
        // Post the manager instead of the callback itself. Cancel can then release the
        // callback (and its dialog references) even after this work has been queued.
        // Posting also lets a Button finish executing its command before removal.
        Dispatcher.UIThread.Post(InvokeCompleted);
    }

    private void InvokeCompleted()
    {
        Action? completed;
        lock (_sync)
        {
            completed = _completed;
            _completed = null;
        }

        completed?.Invoke();
    }
}
