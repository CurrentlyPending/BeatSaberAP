using Zenject;

internal class InjectInstaller : Installer {
    public override void InstallBindings() {
        Container.BindInterfacesAndSelfTo<LockHooks>().AsSingle();
        Container.BindInterfacesAndSelfTo<MenuButtonManager>().AsSingle();
        Container.Bind<ArchipelagoFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
    }
}
