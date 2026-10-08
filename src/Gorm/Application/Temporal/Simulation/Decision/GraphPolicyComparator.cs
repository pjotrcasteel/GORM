using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Compares application-owned policies with transparent weighted normalized criteria.
/// </summary>
public static class GraphPolicyComparator
{
    /// <summary>
    /// Evaluates and ranks policy candidates against one immutable world.
    /// </summary>
    public static GraphPolicyComparisonResult Compare(
        GraphWorldSnapshot baseline,
        IReadOnlyCollection<GraphPolicyCandidate> candidates,
        IReadOnlyCollection<GraphPolicyCriterion> criteria,
        GraphPolicyComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(criteria);
        options ??= new GraphPolicyComparisonOptions();
        Validate(candidates, criteria, options);
        var orderedCandidates = candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
        var orderedCriteria = criteria.OrderBy(criterion => criterion.Name, StringComparer.Ordinal).ToArray();
        var values = EvaluateCandidates(baseline, orderedCandidates, orderedCriteria, cancellationToken);

        var ranges = orderedCriteria.ToDictionary(
            criterion => criterion.Name,
            criterion =>
            {
                var criterionValues = orderedCandidates.Select(candidate => values[candidate.Id][criterion.Name]).ToArray();
                return (Minimum: criterionValues.Min(), Maximum: criterionValues.Max());
            },
            StringComparer.Ordinal);
        var totalWeight = orderedCriteria.Sum(criterion => criterion.Weight);
        var unranked = orderedCandidates.Select(candidate =>
        {
            var evidence = orderedCriteria.Select(criterion =>
            {
                var value = values[candidate.Id][criterion.Name];
                var (Minimum, Maximum) = ranges[criterion.Name];
                var utility = CalculateUtility(value, Minimum, Maximum, criterion.Goal);
                return new GraphPolicyCriterionScore
                {
                    CriterionName = criterion.Name,
                    Value = value,
                    Utility = utility,
                    WeightedUtility = utility * criterion.Weight
                };
            }).ToArray();
            var score = evidence.Sum(item => item.WeightedUtility) / totalWeight;
            return (candidate.Id, Score: score, Evidence: evidence);
        })
        .OrderByDescending(item => item.Score)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .ToArray();
        var ranking = unranked.Select((item, index) => new GraphPolicyScore
        {
            Rank = index + 1,
            CandidateId = item.Id,
            Score = item.Score,
            Criteria = item.Evidence,
            Explanation = $"Policy '{item.Id}' ranks {index + 1} with normalized weighted utility {item.Score:F6}."
        }).ToArray();
        return new GraphPolicyComparisonResult(ranking);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, double>> EvaluateCandidates(
        GraphWorldSnapshot baseline,
        IReadOnlyCollection<GraphPolicyCandidate> candidates,
        IReadOnlyCollection<GraphPolicyCriterion> criteria,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metrics = candidate.Evaluate(baseline)
                ?? throw new InvalidOperationException($"Policy '{candidate.Id}' returned null metrics.");
            foreach (var criterionName in criteria.Select(criterion => criterion.Name))
            {
                if (!metrics.TryGetValue(criterionName, out var value))
                {
                    throw new InvalidOperationException($"Policy '{candidate.Id}' did not return criterion '{criterionName}'.");
                }

                if (!double.IsFinite(value))
                {
                    throw new InvalidOperationException($"Policy '{candidate.Id}' returned non-finite criterion '{criterionName}'.");
                }
            }

            values.Add(candidate.Id, metrics);
        }

        return values;
    }

    private static double CalculateUtility(double value, double minimum, double maximum, GraphPolicyGoal goal)
    {
        if (Math.Abs(maximum - minimum) < 1e-12)
        {
            return 1;
        }

        return goal == GraphPolicyGoal.Maximize ? (value - minimum) / (maximum - minimum) : (maximum - value) / (maximum - minimum);
    }

    private static void Validate(IReadOnlyCollection<GraphPolicyCandidate> candidates, IReadOnlyCollection<GraphPolicyCriterion> criteria, GraphPolicyComparisonOptions options)
    {
        if (options.MaximumCandidates <= 0 || options.MaximumCriteria <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (candidates.Count == 0 || criteria.Count == 0)
        {
            throw new ArgumentException("At least one policy candidate and criterion are required.");
        }

        if (candidates.Count > options.MaximumCandidates || criteria.Count > options.MaximumCriteria)
        {
            throw new InvalidOperationException("Policy comparison exceeds configured candidate or criterion limits.");
        }

        if (candidates.Any(candidate => string.IsNullOrWhiteSpace(candidate.Id) || candidate.Evaluate is null) ||
            candidates.Select(candidate => candidate.Id).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            throw new ArgumentException("Policy ids must be non-empty and unique and evaluators are required.", nameof(candidates));
        }

        if (criteria.Any(criterion => string.IsNullOrWhiteSpace(criterion.Name) || !double.IsFinite(criterion.Weight) || criterion.Weight <= 0) ||
            criteria.Select(criterion => criterion.Name).Distinct(StringComparer.Ordinal).Count() != criteria.Count)
        {
            throw new ArgumentException("Criterion names must be unique and weights must be finite and positive.", nameof(criteria));
        }
    }
}