using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MyMemories.Commands;

/// <summary>
/// An async command whose sole purpose is to relay its functionality to other objects by invoking async delegates.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Initializes a new instance of AsyncRelayCommand.
    /// </summary>
    /// <param name="execute">The async execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    public bool IsExecuting => _isExecuting;

    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">This parameter will always be ignored.</param>
    /// <returns>true if the command can execute; otherwise, false.</returns>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute());
    }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="parameter">This parameter will always be ignored.</param>
    public async void Execute(object? parameter)
    {
        await ExecuteAsync();
    }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// An async command whose sole purpose is to relay its functionality to other objects by invoking async delegates.
/// Generic version that accepts a parameter.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Initializes a new instance of AsyncRelayCommand.
    /// </summary>
    /// <param name="execute">The async execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    public bool IsExecuting => _isExecuting;

    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">Data used by the command.</param>
    /// <returns>true if the command can execute; otherwise, false.</returns>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute((T?)parameter));
    }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="parameter">Data used by the command.</param>
    public async void Execute(object? parameter)
    {
        await ExecuteAsync((T?)parameter);
    }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="parameter">Data used by the command.</param>
    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
