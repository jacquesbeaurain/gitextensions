using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GitUI.Compat;

/// <summary>
/// Mirrors an in-window <see cref="Menu"/> into the window's <see cref="NativeMenu"/>, so that platforms
/// with a global menu bar (macOS) show the menus there instead of inside the window.
/// </summary>
/// <remarks>
/// The source menu stays the single source of truth: native clicks are forwarded to the source items,
/// and header, enabled, visible, checked and icon changes are copied across. Submenus are rebuilt each
/// time they open, after the source item's <c>SubmenuOpened</c> handlers have run, so lazily populated
/// menus stay current.
/// </remarks>
internal static class NativeMenuMirror
{
    /// <summary>
    /// Mirrors <paramref name="source"/> into the native menu bar of <paramref name="window"/> and hides it.
    /// Does nothing on platforms without a global menu bar or when not running as the desktop application.
    /// </summary>
    public static void AttachIfSupported(Window window, Menu source)
    {
        // Headless hosts (tests, previewer) have no global menu bar and keep the in-window menu.
        if (!OperatingSystem.IsMacOS()
            || Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
        {
            return;
        }

        Attach(window, source);
    }

    internal static NativeMenu Attach(Window window, Menu source)
    {
        NativeMenu nativeMenu = new();
        Populate(nativeMenu, source.Items);
        source.Items.CollectionChanged += (_, _) => Populate(nativeMenu, source.Items);
        NativeMenu.SetMenu(window, nativeMenu);
        source.IsVisible = false;
        return nativeMenu;
    }

    private static void Populate(NativeMenu target, IEnumerable<object?> sourceItems)
    {
        target.Items.Clear();
        foreach (object? sourceItem in sourceItems)
        {
            switch (sourceItem)
            {
                case MenuItem menuItem:
                    target.Items.Add(CreateItem(menuItem));
                    break;
                case Separator:
                    target.Items.Add(new NativeMenuItemSeparator());
                    break;
            }
        }
    }

    private static NativeMenuItem CreateItem(MenuItem source)
    {
        NativeMenuItem item = new();
        CopyState(source, item);

        source.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.HeaderProperty
                || e.Property == MenuItem.IconProperty
                || e.Property == MenuItem.InputGestureProperty
                || e.Property == MenuItem.IsCheckedProperty
                || e.Property == MenuItem.ToggleTypeProperty
                || e.Property == MenuItem.IsEnabledProperty
                || e.Property == MenuItem.IsVisibleProperty)
            {
                CopyState(source, item);
            }
        };

        item.Click += (_, _) => ForwardClick(source, item);

        EnsureSubmenu(source, item);
        source.Items.CollectionChanged += (_, _) =>
        {
            EnsureSubmenu(source, item);
            if (item.Menu is not null)
            {
                Populate(item.Menu, source.Items);
            }
        };

        return item;
    }

    private static void ForwardClick(MenuItem source, NativeMenuItem item)
    {
        // The native item has already toggled itself; bring the source in line before its handlers run.
        if (source.ToggleType != MenuItemToggleType.None)
        {
            source.IsChecked = item.IsChecked;
        }

        source.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (source.Command?.CanExecute(source.CommandParameter) == true)
        {
            source.Command.Execute(source.CommandParameter);
        }
    }

    private static void EnsureSubmenu(MenuItem source, NativeMenuItem item)
    {
        if (item.Menu is not null || source.Items.Count == 0)
        {
            return;
        }

        NativeMenu menu = new();
        Populate(menu, source.Items);

        // Let the source's SubmenuOpened handlers fill in dynamic entries before the native menu is shown.
        menu.Opening += (_, _) =>
        {
            source.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
            Populate(menu, source.Items);
        };
        item.Menu = menu;
    }

    private static void CopyState(MenuItem source, NativeMenuItem target)
    {
        target.Header = StripMnemonic(source.Header as string ?? source.Header?.ToString() ?? string.Empty);
        target.IsEnabled = source.IsEnabled;
        target.IsVisible = source.IsVisible;
        target.Gesture = source.InputGesture;
        target.ToggleType = source.ToggleType;
        target.IsChecked = source.IsChecked;
        target.Icon = (source.Icon as Image)?.Source as Bitmap;
    }

    /// <summary>
    /// Converts an access-key header ("_File", "Save __as") to plain text; macOS menus have no mnemonics.
    /// </summary>
    internal static string StripMnemonic(string header)
    {
        System.Text.StringBuilder result = new(header.Length);
        for (int i = 0; i < header.Length; i++)
        {
            if (header[i] == '_')
            {
                if (i + 1 < header.Length && header[i + 1] == '_')
                {
                    result.Append('_');
                    i++;
                }

                continue;
            }

            result.Append(header[i]);
        }

        return result.ToString();
    }
}
