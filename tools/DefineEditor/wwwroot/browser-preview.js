// The browser preview's "Field names" option: an empty editor shows its field name, as in the editor's own preview.
// The ids of the inputs are "<prefix>-<field name>" (FormLayoutPresenter.IdPrefix). Names written here are marked,
// so turning the option off removes them and leaves a placeholder the editor rendered itself.
window.polhemBrowserPreview = {
    showFieldNames: function (prefix, show) {
        document.querySelectorAll('[data-polhem-field-name]').forEach(function (e) {
            e.removeAttribute('placeholder');
            e.removeAttribute('data-polhem-field-name');
        });
        if (!show) return;
        document.querySelectorAll('[id^="' + prefix + '-"]').forEach(function (e) {
            if (e.getAttribute('placeholder')) return;
            e.setAttribute('placeholder', e.id.substring(prefix.length + 1));
            e.setAttribute('data-polhem-field-name', '');
        });
    }
};
