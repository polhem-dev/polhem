using Avalonia.DemoCenter.Modules.ControlTypes;
using Avalonia.DemoCenter.Modules.DataBinding;
using Avalonia.DemoCenter.Modules.FormModes;
using Avalonia.DemoCenter.Modules.Grids;
using Avalonia.DemoCenter.Modules.Layouts;
using Avalonia.DemoCenter.Modules.Lookup;
using Avalonia.DemoCenter.Modules.MasterDetail;
using Avalonia.DemoCenter.Modules.Permissions;
using Avalonia.DemoCenter.Modules.ReadOnlyRequired;

namespace Avalonia.DemoCenter.Modules
{
    /// <summary>
    /// Central registry of every demo module. The navigation tree is generated from this
    /// list, grouped by <see cref="IDemoModule.Category"/> (theme) into a two-level tree
    /// (theme → case), so registering a new module here is all it takes to surface it.
    /// </summary>
    public static class DemoModuleRegistry
    {
        /// <summary>All registered demo modules, in navigation order.</summary>
        public static IReadOnlyList<IDemoModule> Modules { get; } =
        [
            // Control Types.
            new ControlGalleryModule(),
            new FieldControlComparisonModule(),
            new TableControlComparisonModule(),
            // Data Binding.
            new AmbientBindingModule(),
            new ExplicitBindingModule(),
            new TwoWaySyncModule(),
            new DataObjectEventsModule(),
            // Read-only & Required.
            new ReadOnlyFieldModule(),
            new RequiredReadOnlyMarkModule(),
            // FormMode States.
            new InteractiveFormModeModule(),
            new FormModeStatesModule(),
            new GridFormModeModule(),
            // Lookup.
            new LookupPickerModule(),
            // Layout.
            new AutoFormLayoutModule(),
            new MultiColumnLayoutModule(),
            // Grid.
            new InCellEditModule(),
            new EditFormModule(),
            new AmbientGridModule(),
            new ListModeModule(),
            new NumberFormatModule(),
            new MultiCurrencyModule(),
            new MultiUnitModule(),
            // Master-Detail.
            new MasterDetailModule(),
            // Permission Capability (front-end permission degradation).
            new PermissionCapabilityModule(),
        ];
    }
}
