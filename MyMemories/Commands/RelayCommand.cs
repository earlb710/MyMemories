using System;
using System.Windows.Input;

namespace MyMemories.Commands;

/// <summary>
/// A command whose sole purpose is to relay its functionality to other objects by invoking delegates.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of RelayCommand.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">This parameter will always be ignored.</param>
    /// <returns>true if the command can execute; otherwise, false.</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="parameter">This parameter will always be ignored.</param>
    public void Execute(object? parameter)
    {
        _execute();
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
/// A command whose sole purpose is to relay its functionality to other objects by invoking delegates.
/// Generic version that accepts a parameter.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of RelayCommand.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">Data used by the command.</param>
    /// <returns>true if the command can execute; otherwise, false.</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute((T?)parameter);
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="parameter">Data used by the command.</param>
    public void Execute(object? parameter)
    {
        _execute((T?)parameter);
    }

    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
