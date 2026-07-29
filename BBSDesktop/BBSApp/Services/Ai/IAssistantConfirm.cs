namespace BBSApp.Services.Ai;

/// <summary>
/// UI confirmation gate for assistant actions that change project state. The main
/// window implements this with a modal dialog, so no assistant-driven mutation is
/// applied without the user's explicit approval.
/// </summary>
public interface IAssistantConfirm
{
    /// <summary>Ask the user to approve a change. Returns true only if approved.</summary>
    Task<bool> ConfirmAsync(string title, string details);
}
