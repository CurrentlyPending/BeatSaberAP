using BeatSaberMarkupLanguage;
using HMUI;
using Zenject;

public class ArchipelagoFlowCoordinator : FlowCoordinator {
    #pragma warning disable 0649
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator;
    #pragma warning restore 0649
    private ArchipelagoViewController _apViewCtr;

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
        _apViewCtr ??= BeatSaberUI.CreateViewController<ArchipelagoViewController>();
        if (firstActivation) {
            SetTitle("Archipelago Connection Settings");
            showBackButton = true;
        }
        if (addedToHierarchy) {
            ProvideInitialViewControllers(_apViewCtr);
        }
    }

    protected override void BackButtonWasPressed(ViewController topViewController) {
        _apViewCtr.connStatus.text = "";
        _mainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
