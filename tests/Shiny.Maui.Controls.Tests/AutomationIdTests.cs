using System.Text.RegularExpressions;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Shiny.Maui.Controls.Infrastructure;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Path = System.IO.Path;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A control's parts are named after its <see cref="Element.AutomationId"/> so a UI test can reach
/// them (see <see cref="AutomationIds"/>). These guard the two ways that goes wrong: a control that
/// throws when its id changes (the Expander did, because the AutomationId setter only works once), and
/// a part left carrying a fixed or stale id that collides between two copies of the control.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class AutomationIdTests(ITestOutputHelper output)
{
    /// <summary>
    /// <c>x.AutomationId = …</c> or <c>AutomationId = …</c> in an initializer. The lookbehind skips
    /// longer names that end the same way (<c>CheckBoxAutomationId</c>).
    /// </summary>
    static readonly Regex SetterUse = new(@"(?<![A-Za-z0-9_])AutomationId\s*=(?![=>])", RegexOptions.Compiled);

    /// <summary>A declaration rather than a write: a config record's <c>string? AutomationId = null</c>.</summary>
    static readonly Regex Declaration = new(@"\bstring\??\s+AutomationId\s*=", RegexOptions.Compiled);


    [Fact]
    public void NoControlUsesTheAutomationIdSetter()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            if (Path.GetFileName(file) == "AutomationIds.cs")
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("//") || Declaration.IsMatch(line) || !SetterUse.IsMatch(line))
                    continue;

                offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line}");
            }
        }

        foreach (var o in offenders)
            output.WriteLine("SETTER: " + o);

        offenders.ShouldBeEmpty(
            "The AutomationId setter throws the second time it is used, and a part's id changes " +
            "whenever its owner's does. Name the part with AutomationIds.Set / Link / Track.");
    }


    [Fact]
    public void EveryControlFollowsItsOwnAutomationId()
    {
        var broken = new List<string>();

        // Constructing every control starts the odd entrance animation from an async void method. On
        // xUnit's own context a void test waits for those to finish, and in a headless host they never
        // do, so the test would hang. Run them on a context that just executes and records them.
        var previousContext = SynchronizationContext.Current;
        var capturing = new CapturingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(capturing);
        try
        {
            Probe(broken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        foreach (var b in broken)
            output.WriteLine("BROKEN: " + b);

        broken.ShouldBeEmpty();
    }


    void Probe(List<string> broken)
    {
        foreach (var type in Controls())
        {
            new Application();
            VisualElement control;
            try
            {
                control = (VisualElement)Activator.CreateInstance(type)!;
            }
            catch
            {
                output.WriteLine($"skipped (cannot construct bare): {type.Name}");
                continue;
            }

            try
            {
                // Set, change, clear, set again: what a binding does when a list rebuild re-binds a row.
                foreach (var id in new[] { "first", "second", null, "third" })
                {
                    if (id is null)
                        control.ClearValue(Element.AutomationIdProperty);
                    else
                        control.SetValue(Element.AutomationIdProperty, id);

                    if (id is null)
                        continue;

                    var stray = Descendants(control)
                        .Select(e => e.AutomationId)
                        .Where(a => !String.IsNullOrEmpty(a) && !a.StartsWith(id, StringComparison.Ordinal))
                        .ToList();

                    if (stray.Count > 0)
                        broken.Add($"{type.Name}: with id '{id}' parts still carry {String.Join(", ", stray.Distinct())}");
                }
            }
            catch (Exception ex)
            {
                broken.Add($"{type.Name}: {ex.GetType().Name} {ex.Message}");
            }
        }
    }


    [Fact]
    public void ARebindOfABoundAutomationIdDoesNotThrow()
    {
        // Issue #27: a templated expander re-bound by a BindableLayout rebuild.
        new Application();
        var expander = new Expander { Header = new Label { Text = "Header" }, Content = new Label { Text = "Content" } };
        expander.SetBinding(VisualElement.AutomationIdProperty, nameof(Row.Id));

        expander.BindingContext = new Row("row-1");
        expander.BindingContext = null;
        expander.BindingContext = new Row("row-2");

        Descendants(expander).ShouldContain(e => e.AutomationId == "row-2" + Expander.HeaderAutomationIdSuffix);
    }


    [Fact]
    public void TwoWizardsOnAPageGetTheirOwnButtonIds()
    {
        new Application();
        var first = new Wizard { AutomationId = "checkout" };
        var second = new Wizard { AutomationId = "signup" };

        Ids(first).ShouldContain("checkout" + Wizard.NextAutomationIdSuffix);
        Ids(second).ShouldContain("signup" + Wizard.NextAutomationIdSuffix);
        Ids(second).ShouldContain("signup" + Wizard.BackAutomationIdSuffix);
        Ids(second).ShouldContain("signup" + Wizard.CancelAutomationIdSuffix);
    }


    [Fact]
    public void AWizardWithoutAnIdKeepsTheFixedButtonIds()
    {
        new Application();
        var wizard = new Wizard();

        Ids(wizard).ShouldContain("WizardNextButton");
        Ids(wizard).ShouldContain("WizardBackButton");
        Ids(wizard).ShouldContain("WizardCancelButton");
    }


    [Fact]
    public void ATapTargetIsPressedThroughTheSamePathAsATap()
    {
        new Application();
        var clicks = 0;
        var button = new ShinyButton { Text = "Save" };
        button.Clicked += (_, _) => clicks++;

        ((ITapTarget)button).Invoke();
        clicks.ShouldBe(1);

        // A disabled button announces itself as one, and pressing it from UI Automation does nothing.
        button.IsEnabled = false;
        ((ITapTarget)button).Invoke();
        clicks.ShouldBe(1);
    }


    [Fact]
    public void ADisabledFabIgnoresAnAutomationPress()
    {
        new Application();
        var clicks = 0;
        var fab = new Fab { Text = "New" };
        fab.Clicked += (_, _) => clicks++;

        ((ITapTarget)fab).Invoke();
        fab.IsEnabled = false;
        ((ITapTarget)fab).Invoke();

        clicks.ShouldBe(1);
    }


    record Row(string Id);


    sealed class CapturingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => Invoke(d, state);

        public override void Send(SendOrPostCallback d, object? state) => Invoke(d, state);

        static void Invoke(SendOrPostCallback d, object? state)
        {
            try
            {
                d(state);
            }
            catch
            {
                // A headless host has no animation manager; that is the harness, not the control.
            }
        }
    }


    static IEnumerable<string> Ids(Element root) => Descendants(root).Select(e => e.AutomationId).Where(a => a is not null);


    static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }


    static IEnumerable<Type> Controls() => typeof(AutoCompleteEntry).Assembly
        .GetTypes()
        .Where(t => t is { IsPublic: true, IsAbstract: false, IsGenericTypeDefinition: false })
        .Where(t => typeof(View).IsAssignableFrom(t))
        .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
        .OrderBy(t => t.Name);


    static IEnumerable<string> SourceFiles()
        => Directory.EnumerateFiles(FindLibraryRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f =>
                !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));


    static string FindLibraryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Shiny.Maui.Controls");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("src/Shiny.Maui.Controls");
    }
}
