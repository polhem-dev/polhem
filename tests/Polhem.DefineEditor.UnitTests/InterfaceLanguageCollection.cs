using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// Tests that change <see cref="LocalizationService.Culture"/>, which every test reads: they run alone, after the others.
/// </summary>
[CollectionDefinition(nameof(InterfaceLanguageCollection), DisableParallelization = true)]
public sealed class InterfaceLanguageCollection;
