using System.ComponentModel;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Connectors;
using Polhem.Web.Blazor.Controls;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's grid column filter must do: the rules of <see cref="GridColumnFilterBase"/>, seen through
    /// each package's markup.
    /// </summary>
    public abstract class GridColumnFilterBehaviorTests : BunitContext, IAsyncLifetime
    {
        private const string Form = "form.polhem-grid-filter__form";
        private const string FirstInput = ".polhem-grid-filter__first input";
        private const string Error = ".polhem-grid-filter__error";

        private readonly FormField _field = new("qty", "Quantity", FieldDbType.Integer);
        private readonly ListFilterState _filters = new();
        private readonly List<ColumnFilterChange> _applied = [];

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        /// <summary>Renders what the package needs before the filter, such as the host its popovers draw into.</summary>
        protected virtual void OnBeforeRender()
        {
        }

        /// <summary>Finds the elements of the open panel, wherever the package draws it.</summary>
        /// <param name="cut">The rendered filter.</param>
        /// <param name="selector">The CSS selector.</param>
        /// <returns>The matching elements.</returns>
        protected virtual IReadOnlyList<IElement> FindInPanel(IRenderedComponent<ContainerFragment> cut, string selector)
            => cut.FindAll(selector);

        /// <summary>
        /// Retries <paramref name="assertion"/> until the panel's renders satisfy it. A package that draws the panel in a
        /// host of its own (MudBlazor's popover provider) renders it a render after the filter, so a check made right
        /// after an action can run before the panel shows the result.
        /// </summary>
        /// <param name="cut">The rendered filter.</param>
        /// <param name="assertion">The check on the panel.</param>
        protected virtual void WaitForPanel(IRenderedComponent<ContainerFragment> cut, Action assertion)
            => cut.WaitForAssertion(assertion, PanelTimeout);

        /// <summary>Gets how long a panel check is retried.</summary>
        protected static TimeSpan PanelTimeout { get; } = TimeSpan.FromSeconds(5);

        /// <summary>Gets the filter component, which derives from <see cref="GridColumnFilterBase"/>.</summary>
        protected abstract Type FilterType { get; }

        /// <summary>Gets the selector of the button that opens the panel.</summary>
        protected virtual string OpenButton => ".polhem-grid-filter__button";

        /// <summary>Gets the selector of the button that clears the filter.</summary>
        protected virtual string ClearButton => ".polhem-grid-filter__clear";

        /// <summary>Types <paramref name="text"/> into the first box the way the package's input takes it.</summary>
        /// <param name="input">The first box's input element.</param>
        /// <param name="text">The text to enter.</param>
        protected abstract void EnterFirst(IElement input, string text);

        private IElement InPanel(IRenderedComponent<ContainerFragment> cut, string selector)
        {
            IElement? found = null;
            WaitForPanel(cut, () => found = Assert.Single(FindInPanel(cut, selector)));
            return found!;
        }

        private IRenderedComponent<ContainerFragment> RenderFilter()
        {
            OnBeforeRender();
            var type = FilterType;
            return Render(b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(GridColumnFilterBase.Column), new LayoutColumn { FieldName = _field.FieldName, Caption = _field.Caption });
                b.AddAttribute(2, nameof(GridColumnFilterBase.Field), _field);
                b.AddAttribute(3, nameof(GridColumnFilterBase.Kind), ColumnFilterKind.Number);
                b.AddAttribute(4, nameof(GridColumnFilterBase.Filters), _filters);
                b.AddAttribute(5, nameof(GridColumnFilterBase.Applied),
                    EventCallback.Factory.Create<ColumnFilterChange>(this, change => _applied.Add(change)));
                b.CloseComponent();
            });
        }

        private IRenderedComponent<ContainerFragment> RenderOpen()
        {
            var cut = RenderFilter();
            cut.Find(OpenButton).Click();
            return cut;
        }

        [Fact]
        [DisplayName("Opening the panel fills it with what was entered before")]
        public void Open_FillsThePreviousEntry()
        {
            Assert.True(_filters.TrySet(_field, ColumnFilterKind.Number, "2", "5"));

            var cut = RenderOpen();

            WaitForPanel(cut, () => Assert.Equal("2", Assert.Single(FindInPanel(cut, FirstInput)).GetAttribute("value")));
        }

        [Fact]
        [DisplayName("An entry the filters cannot read shows an error, sends nothing and keeps the panel open")]
        public void Apply_InvalidEntry_IsRefused()
        {
            var cut = RenderOpen();
            EnterFirst(InPanel(cut, FirstInput), "abc");

            InPanel(cut, Form).Submit();

            Assert.Empty(_applied);
            WaitForPanel(cut, () => Assert.Single(FindInPanel(cut, Error)));
            WaitForPanel(cut, () => Assert.Single(FindInPanel(cut, Form)));
        }

        [Fact]
        [DisplayName("A valid entry is sent and closes the panel")]
        public void Apply_ValidEntry_IsSentAndCloses()
        {
            var cut = RenderOpen();
            EnterFirst(InPanel(cut, FirstInput), "3");

            InPanel(cut, Form).Submit();

            var change = Assert.Single(_applied);
            Assert.Equal("3", change.First);
            Assert.Same(_field, change.Field);
            WaitForPanel(cut, () => Assert.Empty(FindInPanel(cut, Form)));
        }

        [Fact]
        [DisplayName("Clearing sends an empty entry, which removes the column's filter")]
        public void Clear_SendsAnEmptyEntry()
        {
            Assert.True(_filters.TrySet(_field, ColumnFilterKind.Number, "2", "5"));
            var cut = RenderOpen();

            InPanel(cut, ClearButton).Click();

            var change = Assert.Single(_applied);
            Assert.Equal(string.Empty, change.First);
            Assert.Equal(string.Empty, change.Second);
        }

        [Fact]
        [DisplayName("Escape closes the panel without sending what was typed")]
        public void Escape_ClosesWithoutSending()
        {
            var cut = RenderOpen();
            EnterFirst(InPanel(cut, FirstInput), "3");

            InPanel(cut, Form).KeyDown("Escape");

            Assert.Empty(_applied);
            WaitForPanel(cut, () => Assert.Empty(FindInPanel(cut, Form)));
        }

        [Fact]
        [DisplayName("The button closes an open panel, and opening it again shows no error from before")]
        public void Toggle_Twice_ClosesAndForgetsTheError()
        {
            var cut = RenderOpen();
            EnterFirst(InPanel(cut, FirstInput), "abc");
            InPanel(cut, Form).Submit();
            WaitForPanel(cut, () => Assert.Single(FindInPanel(cut, Error)));

            cut.Find(OpenButton).Click();
            WaitForPanel(cut, () => Assert.Empty(FindInPanel(cut, Form)));

            cut.Find(OpenButton).Click();
            WaitForPanel(cut, () =>
            {
                Assert.Single(FindInPanel(cut, Form));
                Assert.Empty(FindInPanel(cut, Error));
            });
        }

        [Fact]
        [DisplayName("After Escape the panel opens with the applied entry again, not what was typed")]
        public void Escape_ThenReopen_ShowsTheAppliedEntry()
        {
            Assert.True(_filters.TrySet(_field, ColumnFilterKind.Number, "2", "5"));
            var cut = RenderOpen();
            EnterFirst(InPanel(cut, FirstInput), "9");
            InPanel(cut, Form).KeyDown("Escape");

            cut.Find(OpenButton).Click();

            WaitForPanel(cut, () => Assert.Equal("2", Assert.Single(FindInPanel(cut, FirstInput)).GetAttribute("value")));
        }
    }
}
