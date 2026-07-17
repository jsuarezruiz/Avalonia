using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Controls;

/// <summary>
/// Describes the semantic icon requested for a message dialog.
/// </summary>
public enum MessageDialogIcon
{
    /// <summary>No icon is requested.</summary>
    None,

    /// <summary>Informational content.</summary>
    Information,

    /// <summary>A successful outcome.</summary>
    Success,

    /// <summary>A warning that does not necessarily indicate failure.</summary>
    Warning,

    /// <summary>An error or failed operation.</summary>
    Error,

    /// <summary>A question that requires a choice.</summary>
    Question
}

/// <summary>
/// Describes why a message dialog completed.
/// </summary>
public enum MessageDialogDismissReason
{
    /// <summary>An action was invoked.</summary>
    Action,
    /// <summary>The dialog was dismissed without an action.</summary>
    Dismissed,

    /// <summary>The owning top level closed.</summary>
    OwnerClosed
}

/// <summary>
/// Defines an action displayed by a message dialog.
/// </summary>
public sealed class MessageDialogAction
{
    /// <summary>
    /// Initializes a new message-dialog action.
    /// </summary>
    /// <param name="id">A stable application-defined identifier.</param>
    /// <param name="text">The localized action label.</param>
    public MessageDialogAction(string id, string text)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An action ID cannot be empty or whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Action text cannot be empty or whitespace.", nameof(text));

        Id = id;
        Text = text;
    }

    /// <summary>Gets the stable application-defined action identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the localized action text.</summary>
    public string Text { get; }

    /// <summary>Gets or sets whether this is the default action.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Gets or sets whether this is the cancel action.</summary>
    public bool IsCancel { get; init; }

    /// <summary>Gets or sets whether this action is destructive.</summary>
    public bool IsDestructive { get; init; }
}

/// <summary>
/// Defines the data shown by a message dialog.
/// </summary>
public sealed class MessageDialogOptions
{
    /// <summary>
    /// Initializes message-dialog options.
    /// </summary>
    /// <param name="message">The primary message.</param>
    /// <param name="actions">The actions to display.</param>
    public MessageDialogOptions(string message, params MessageDialogAction[] actions)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A message cannot be empty or whitespace.", nameof(message));
        ArgumentNullException.ThrowIfNull(actions);
        Message = message;
        Actions = Array.AsReadOnly(actions.ToArray());
    }

    /// <summary>Gets the primary message text.</summary>
    public string Message { get; }

    /// <summary>Gets or sets the optional dialog title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets or sets optional supporting text.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets or sets the semantic icon hint.</summary>
    public MessageDialogIcon Icon { get; init; }

    /// <summary>Gets the actions shown by the dialog.</summary>
    public IReadOnlyList<MessageDialogAction> Actions { get; }
}

/// <summary>
/// Represents the result of a message dialog.
/// </summary>
public sealed class MessageDialogResult
{
    /// <summary>
    /// Initializes a message-dialog result.
    /// </summary>
    /// <param name="actionId">The selected action ID, or null for dismissal.</param>
    /// <param name="dismissReason">The reason the dialog completed.</param>
    public MessageDialogResult(string? actionId, MessageDialogDismissReason dismissReason)
    {
        if (!Enum.IsDefined(dismissReason))
            throw new ArgumentOutOfRangeException(nameof(dismissReason));
        if (dismissReason == MessageDialogDismissReason.Action && string.IsNullOrWhiteSpace(actionId))
            throw new ArgumentException("An action result must include an action ID.", nameof(actionId));
        if (dismissReason != MessageDialogDismissReason.Action && actionId is not null)
            throw new ArgumentException("A non-action result cannot include an action ID.", nameof(actionId));

        ActionId = actionId;
        DismissReason = dismissReason;
    }

    /// <summary>Gets the invoked action identifier, or null if no action was invoked.</summary>
    public string? ActionId { get; }

    /// <summary>Gets why the dialog completed.</summary>
    public MessageDialogDismissReason DismissReason { get; }
}

internal static class MessageDialogValidation
{
    public static void Validate(MessageDialogOptions options)
    {
        if (!Enum.IsDefined(options.Icon))
            throw new ArgumentException("Message dialog icon is invalid.", nameof(options));

        if (options.Actions.Count is < 1 or > 3)
            throw new ArgumentException("A message dialog must define between one and three actions.", nameof(options));

        if (options.Actions.Any(x => x is null))
            throw new ArgumentException("Message dialog actions cannot contain null.", nameof(options));

        if (options.Actions.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != options.Actions.Count)
            throw new ArgumentException("Message dialog action IDs must be unique.", nameof(options));

        if (options.Actions.Count(x => x.IsDefault) > 1)
            throw new ArgumentException("A message dialog can define only one default action.", nameof(options));

        if (options.Actions.Count(x => x.IsCancel) > 1)
            throw new ArgumentException("A message dialog can define only one cancel action.", nameof(options));
    }

    public static void ValidateResult(MessageDialogOptions options, MessageDialogResult result)
    {
        if (result.DismissReason == MessageDialogDismissReason.Action &&
            !options.Actions.Any(action => action.Id == result.ActionId))
        {
            throw new InvalidOperationException(
                "The message-dialog provider returned an action that was not requested.");
        }
    }
}
