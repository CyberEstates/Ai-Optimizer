using System.Windows;
using System.Windows.Controls;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.ViewModels;

namespace AIGamingOptimizer.Views;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
        AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(OnCheckClicked), true);
    }

    private void OnCheckClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (e.OriginalSource is CheckBox { DataContext: StartupEntry entry } &&
            DataContext is StartupViewModel vm)
        {
            vm.ToggleCommand.Execute(entry);
        }
    }
}
