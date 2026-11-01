using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Components;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared form page behaviors on the MudBlazor form page. Its dialogs render in the dialog provider.
    /// </summary>
    public class MudFormPageBehaviorTests : FormPageBehaviorTests
    {
        private IRenderedComponent<MudDialogProvider> _dialogs = default!;
        private IRenderedComponent<MudPopoverProvider> _popovers = default!;

        /// <summary>
        /// Initializes a new instance of <see cref="MudFormPageBehaviorTests"/>.
        /// </summary>
        public MudFormPageBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override void OnServicesRegistered()
        {
            _popovers = Render<MudPopoverProvider>();
            _dialogs = Render<MudDialogProvider>();
        }

        /// <inheritdoc />
        protected override Type PageType => typeof(FormPage);

        /// <inheritdoc />
        protected override string RowSelector => "tr.polhem-grid__row";

        /// <inheritdoc />
        protected override string SortSelector => "button.polhem-grid__sort";

        /// <inheritdoc />
        protected override void DoubleClickRow(IElement row) => row.Click(new MouseEventArgs { Detail = 2 });

        /// <inheritdoc />
        protected override void DoubleClickField(IRenderedComponent<ContainerFragment> cut, string fieldName)
            => cut.Find($"#polhem-form-{fieldName}").ParentElement!.Closest(".polhem-button-edit")!.DoubleClick();

        /// <inheritdoc />
        protected override IReadOnlyList<IElement> FindInDialogs(IRenderedComponent<ContainerFragment> cut, string selector)
            => _dialogs.FindAll(selector);

        /// <inheritdoc />
        protected override void ReachCardListEnd(IRenderedComponent<ContainerFragment> cut)
        {
            var end = cut.FindComponent<CardListEnd>();
            end.InvokeAsync(() => end.Instance.ReachedEnd.InvokeAsync());
        }

        /// <inheritdoc />
        protected override void ApplyFirstColumnFilter(IRenderedComponent<ContainerFragment> cut, string text)
        {
            cut.Find(".polhem-grid-filter__button").Click();
            _popovers.WaitForAssertion(() => _popovers.Find(".polhem-grid-filter__first input").Input(text));
            _popovers.Find(".polhem-grid-filter__form").Submit();
        }

        /// <inheritdoc />
        protected override void ChoosePageSize(IRenderedComponent<ContainerFragment> cut, int pageSize)
        {
            var select = cut.FindComponent<MudSelect<int>>();
            select.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(pageSize));
        }
    }
}
