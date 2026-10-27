using System.Collections.Generic;

namespace well_log_reader_backend
{
    /// <summary>
    /// One conditioning step: changes the well and returns what it did.
    /// Steps are small and independent, so they can be added or reordered easily (DESIGN §5).
    /// </summary>
    public interface IConditioningStep
    {
        /// <summary>Applies the step to the well and returns its report entries.</summary>
        List<ReportEntry> Apply(Well well);
    }
}
