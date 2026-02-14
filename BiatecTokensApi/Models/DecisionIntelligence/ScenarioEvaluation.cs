namespace BiatecTokensApi.Models.DecisionIntelligence
{
    /// <summary>
    /// Request for scenario evaluation
    /// </summary>
    public class EvaluateScenarioRequest
    {
        /// <summary>
        /// Asset ID for scenario analysis
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Base scenario inputs (current or historical state)
        /// </summary>
        public ScenarioInputs BaselineInputs { get; set; } = new();

        /// <summary>
        /// Scenario adjustments to model
        /// </summary>
        public ScenarioAdjustments Adjustments { get; set; } = new();

        /// <summary>
        /// Time horizon for scenario projection (in days)
        /// </summary>
        public int ProjectionDays { get; set; } = 30;
    }

    /// <summary>
    /// Baseline inputs for scenario evaluation
    /// </summary>
    public class ScenarioInputs
    {
        /// <summary>
        /// Current unique holders
        /// </summary>
        public int CurrentHolders { get; set; }

        /// <summary>
        /// Current daily transaction volume
        /// </summary>
        public double DailyTransactionVolume { get; set; }

        /// <summary>
        /// Current retention rate (0-100)
        /// </summary>
        public double RetentionRate { get; set; }

        /// <summary>
        /// Current circulating supply
        /// </summary>
        public double CirculatingSupply { get; set; }

        /// <summary>
        /// Current top 10 concentration percentage (0-100)
        /// </summary>
        public double Top10Concentration { get; set; }

        /// <summary>
        /// Historical growth rate (percentage per month)
        /// </summary>
        public double HistoricalGrowthRate { get; set; }
    }

    /// <summary>
    /// Scenario adjustments to model
    /// </summary>
    public class ScenarioAdjustments
    {
        /// <summary>
        /// Expected change in holder growth rate (percentage points)
        /// </summary>
        /// <remarks>
        /// Example: +10 = 10 percentage point increase in growth rate
        /// </remarks>
        public double? HolderGrowthRateDelta { get; set; }

        /// <summary>
        /// Expected change in retention rate (percentage points)
        /// </summary>
        public double? RetentionRateDelta { get; set; }

        /// <summary>
        /// Expected change in transaction volume (percentage)
        /// </summary>
        public double? TransactionVolumeChangePercent { get; set; }

        /// <summary>
        /// Expected change in supply (absolute value)
        /// </summary>
        public double? SupplyChangeDelta { get; set; }

        /// <summary>
        /// Expected whale distribution event (reduces concentration)
        /// </summary>
        public bool? WhaleDistributionEvent { get; set; }

        /// <summary>
        /// External event descriptors for context
        /// </summary>
        public List<string> ExternalEvents { get; set; } = new();
    }

    /// <summary>
    /// Response containing scenario evaluation results
    /// </summary>
    public class ScenarioEvaluationResponse : BaseResponse
    {
        /// <summary>
        /// Asset ID analyzed
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Scenario inputs used
        /// </summary>
        public ScenarioInputs BaselineInputs { get; set; } = new();

        /// <summary>
        /// Adjustments applied
        /// </summary>
        public ScenarioAdjustments AppliedAdjustments { get; set; } = new();

        /// <summary>
        /// Projected outcomes
        /// </summary>
        public ProjectedOutcomes Projections { get; set; } = new();

        /// <summary>
        /// Modeled ranges (optimistic, realistic, pessimistic)
        /// </summary>
        public OutcomeRanges Ranges { get; set; } = new();

        /// <summary>
        /// Key insights from scenario analysis
        /// </summary>
        public List<string> KeyInsights { get; set; } = new();

        /// <summary>
        /// Caveats and assumptions
        /// </summary>
        public List<string> Caveats { get; set; } = new();

        /// <summary>
        /// Metadata about the scenario calculation
        /// </summary>
        public MetricMetadata Metadata { get; set; } = new();
    }

    /// <summary>
    /// Projected outcomes from scenario
    /// </summary>
    public class ProjectedOutcomes
    {
        /// <summary>
        /// Projected unique holders at end of scenario period
        /// </summary>
        public int ProjectedHolders { get; set; }

        /// <summary>
        /// Projected holder growth percentage
        /// </summary>
        public double HolderGrowthPercent { get; set; }

        /// <summary>
        /// Projected retention rate (0-100)
        /// </summary>
        public double ProjectedRetentionRate { get; set; }

        /// <summary>
        /// Projected daily transaction volume
        /// </summary>
        public double ProjectedDailyVolume { get; set; }

        /// <summary>
        /// Projected transaction volume growth percentage
        /// </summary>
        public double VolumeGrowthPercent { get; set; }

        /// <summary>
        /// Projected circulating supply
        /// </summary>
        public double ProjectedCirculatingSupply { get; set; }

        /// <summary>
        /// Projected top 10 concentration (0-100)
        /// </summary>
        public double ProjectedTop10Concentration { get; set; }

        /// <summary>
        /// Projected liquidity score (0-100)
        /// </summary>
        public double ProjectedLiquidityScore { get; set; }

        /// <summary>
        /// Overall health score projection (0-100)
        /// </summary>
        public double ProjectedHealthScore { get; set; }

        /// <summary>
        /// Change in health score from baseline
        /// </summary>
        public double HealthScoreDelta { get; set; }
    }

    /// <summary>
    /// Outcome ranges for optimistic/realistic/pessimistic scenarios
    /// </summary>
    public class OutcomeRanges
    {
        /// <summary>
        /// Optimistic scenario outcomes (75th percentile)
        /// </summary>
        public RangeOutcome Optimistic { get; set; } = new();

        /// <summary>
        /// Realistic scenario outcomes (50th percentile/median)
        /// </summary>
        public RangeOutcome Realistic { get; set; } = new();

        /// <summary>
        /// Pessimistic scenario outcomes (25th percentile)
        /// </summary>
        public RangeOutcome Pessimistic { get; set; } = new();

        /// <summary>
        /// Confidence interval width (higher = more uncertainty)
        /// </summary>
        public double ConfidenceIntervalWidth { get; set; }
    }

    /// <summary>
    /// Outcome values for a scenario range
    /// </summary>
    public class RangeOutcome
    {
        /// <summary>
        /// Projected holders count
        /// </summary>
        public int Holders { get; set; }

        /// <summary>
        /// Projected retention rate (0-100)
        /// </summary>
        public double RetentionRate { get; set; }

        /// <summary>
        /// Projected daily transaction volume
        /// </summary>
        public double DailyVolume { get; set; }

        /// <summary>
        /// Projected liquidity score (0-100)
        /// </summary>
        public double LiquidityScore { get; set; }

        /// <summary>
        /// Projected health score (0-100)
        /// </summary>
        public double HealthScore { get; set; }
    }
}
