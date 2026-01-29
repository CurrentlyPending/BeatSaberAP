using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using System;
using Zenject;

internal class MenuButtonManager : IInitializable, IDisposable {
    #pragma warning disable 0649
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator;
    [Inject] private readonly ArchipelagoFlowCoordinator _apFlowCoordinator;
    #pragma warning restore 0649
    private MenuButton _menuButton;

    public void Initialize() {
        _menuButton = new MenuButton("Archipelago", PresentFlowCoordinator);
        MenuButtons.Instance.RegisterButton(_menuButton);
    }

    private void PresentFlowCoordinator() {
        _mainFlowCoordinator.PresentFlowCoordinator(_apFlowCoordinator);
    }

    public void Dispose() {
        if (MenuButtons.Instance != null && BSMLParser.Instance != null) {
            MenuButtons.Instance.UnregisterButton(_menuButton);
        }
    }
}
