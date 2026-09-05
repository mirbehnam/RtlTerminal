using System.Windows;
using System.Windows.Controls;

namespace RtlTerminal;

public sealed class MenuContainerStyleSelector : StyleSelector
{
    public Style? MenuItemStyle { get; set; }
    public Style? SeparatorStyle { get; set; }
    public override Style? SelectStyle(object item, DependencyObject container) =>
        container is Separator ? SeparatorStyle : container is MenuItem ? MenuItemStyle : null;
}
