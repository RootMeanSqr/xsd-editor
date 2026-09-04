using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XsdEditor.App;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
