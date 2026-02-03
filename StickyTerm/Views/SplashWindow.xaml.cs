using System.Windows;
using System.Windows.Input;

namespace StickyTerm.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => DragMove();
    }
}
