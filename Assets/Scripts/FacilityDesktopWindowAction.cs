using UnityEngine;

/// <summary>Real close controls for the authored desktop-style windows.</summary>
public sealed class FacilityDesktopWindowAction : MonoBehaviour
{
    public enum Action { Quit, Resume, Back, CloseBrowser, MainMenu }
    public FacilityGameSession session;
    public Action action;
    public void Activate()
    {
        if (session == null) return;
        switch (action)
        {
            case Action.Quit: session.QuitGame(); break;
            case Action.Resume: session.ResumeGame(); break;
            case Action.Back: session.BackToTitle(); break;
            case Action.CloseBrowser: session.CancelPlacement(); break;
            case Action.MainMenu: session.ReturnToMainMenu(); break;
        }
    }
}
