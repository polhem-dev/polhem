using UIKit;

namespace Polhem.Northwind.iOS;

/// <summary>
/// iOS head entry point. Hands control to UIKit, which instantiates <see cref="AppDelegate"/>
/// to bootstrap the Avalonia application hosting the shared <c>Polhem.Northwind.UI</c>.
/// </summary>
public class Application
{
    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
