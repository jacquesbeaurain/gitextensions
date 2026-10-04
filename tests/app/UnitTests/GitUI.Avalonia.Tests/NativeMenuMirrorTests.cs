using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using GitUI.Compat;

namespace GitExtensionsTests;

[TestFixture]
public sealed class NativeMenuMirrorTests
{
    [TestCase("_File", "File")]
    [TestCase("Save __as", "Save _as")]
    [TestCase("Plain", "Plain")]
    [TestCase("", "")]
    public void StripMnemonic_should_remove_access_key_markers(string header, string expected)
    {
        NativeMenuMirror.StripMnemonic(header).Should().Be(expected);
    }

    [AvaloniaTest]
    public void Attach_should_mirror_items_state_and_hide_the_source_menu()
    {
        MenuItem child = new() { Header = "_Child" };
        MenuItem parent = new() { Header = "_Parent", Items = { child, new Separator() } };
        Menu menu = new() { Items = { parent } };
        Window window = new() { Content = menu };

        NativeMenu native = NativeMirror(window, menu);

        menu.IsVisible.Should().BeFalse();
        NativeMenuItem nativeParent = (NativeMenuItem)native.Items[0];
        nativeParent.Header.Should().Be("Parent");
        nativeParent.Menu!.Items.Should().HaveCount(2);
        nativeParent.Menu.Items[1].Should().BeOfType<NativeMenuItemSeparator>();

        parent.Header = "_Renamed";
        parent.IsEnabled = false;
        nativeParent.Header.Should().Be("Renamed");
        nativeParent.IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Attach_should_forward_clicks_and_refresh_submenus_when_they_open()
    {
        int clicks = 0;
        MenuItem child = new() { Header = "Child" };
        child.Click += (_, _) => clicks++;
        MenuItem parent = new() { Header = "Parent", Items = { child } };
        parent.SubmenuOpened += (_, _) => parent.Items.Add(new MenuItem { Header = "Late" });
        Menu menu = new() { Items = { parent } };
        Window window = new() { Content = menu };

        NativeMenu native = NativeMirror(window, menu);
        NativeMenu submenu = ((NativeMenuItem)native.Items[0]).Menu!;

        submenu.Items.Should().HaveCount(1);
        InvokeNative(submenu, "INativeMenuExporterEventsImplBridge.RaiseOpening");
        submenu.Items.Should().HaveCount(2);

        InvokeNative(submenu.Items[0], "INativeMenuItemExporterEventsImplBridge.RaiseClicked");
        clicks.Should().Be(1);
    }

    // The platform bridge interfaces are internal to Avalonia; the native menu backend calls them explicitly.
    private static void InvokeNative(object target, string interfaceMethod)
        => target.GetType()
            .GetMethod("Avalonia.Controls." + interfaceMethod, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, null);

    private static NativeMenu NativeMirror(Window window, Menu menu) => NativeMenuMirror.Attach(window, menu);
}
