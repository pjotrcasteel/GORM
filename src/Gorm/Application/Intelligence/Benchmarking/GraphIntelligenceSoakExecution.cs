using Gorm.Application.Intelligence.Algorithms.Centrality;

namespace Gorm.Application.Intelligence.Benchmarking;

internal sealed record GraphIntelligenceSoakExecution(int CompletedIterations, GraphDegreeCentralityResult? FinalDegree);