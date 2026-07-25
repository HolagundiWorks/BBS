namespace BBSApp.Services;

/// <summary>Editable site-practice yields for civil take-off (Settings).</summary>
public sealed class CivilYields
{
    public double BricksPerM3 { get; set; } = 500;
    public double BricksPerM2Half { get; set; } = 55;
    public double MortarFraction { get; set; } = 0.30;
    public double SsmMortarFraction { get; set; } = 0.30;
    public double MortarDryFactor { get; set; } = 1.33;
    public double Wastage { get; set; } = 1.05;
    public double ShutteringWastage { get; set; } = 1.05;
    /// <summary>Openings smaller than this (m²) ignored under IS1200 masonry rule.</summary>
    public double IgnoreOpeningBelowM2 { get; set; } = 0.1;
    /// <summary>
    /// When true, beam formwork omits soffit (sides only: 2×D×L) assuming slab formwork covers the underside.
    /// </summary>
    public bool BeamSlabInterfaceDeduct { get; set; }
}
