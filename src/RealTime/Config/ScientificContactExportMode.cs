namespace RealTime.Config
{
    /// <summary>Storage only; must never affect epidemiological calculations or random streams.</summary>
    public enum ScientificContactExportMode
    {
        Standard = 0,
        FullRaw = 1,
        SummaryOnly = 2,
    }
}
