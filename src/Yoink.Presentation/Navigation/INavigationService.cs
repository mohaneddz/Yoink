namespace Yoink.Presentation.Navigation;

/// <summary>Lets view models request page navigation without depending on the shell directly.</summary>
public interface INavigationService
{
    void Navigate(AppPage page);
}
