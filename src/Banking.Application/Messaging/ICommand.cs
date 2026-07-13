namespace Banking.Application.Messaging;

/// <summary>A write operation that changes state and reports only success/failure.</summary>
public interface ICommand;

/// <summary>A write operation that changes state and returns a value on success.</summary>
public interface ICommand<TResult>;
