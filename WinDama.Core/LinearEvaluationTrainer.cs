using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Trains a linear evaluator from exported position samples using ridge
/// regression. Labels are the sample Evaluation values, normally produced by
/// deeper search or tournament analysis.
/// </summary>
public sealed class LinearEvaluationTrainer
{
    private readonly LinearEvaluationFeatureExtractor featureExtractor;

    public LinearEvaluationTrainer()
        : this(new LinearEvaluationFeatureExtractor())
    {
    }

    public LinearEvaluationTrainer(LinearEvaluationFeatureExtractor featureExtractor)
    {
        this.featureExtractor = featureExtractor ?? throw new ArgumentNullException(nameof(featureExtractor));
    }

    public LinearEvaluationTrainingResult Train(PositionDataset dataset, LinearEvaluationTrainingOptions? options = null)
    {
        if (dataset == null)
        {
            throw new ArgumentNullException(nameof(dataset));
        }

        options ??= new LinearEvaluationTrainingOptions();
        List<PositionSample> samples = dataset.Samples
            .Where(sample => sample.Board != null && sample.Board.Length == 8)
            .Take(options.MaximumSamples <= 0 ? int.MaxValue : options.MaximumSamples)
            .ToList();

        if (samples.Count == 0)
        {
            throw new InvalidOperationException("Cannot train a linear evaluator from an empty dataset.");
        }

        string[] featureNames = LinearEvaluationFeatureExtractor.FeatureNames;
        int coefficientCount = featureNames.Length + 1; // bias + features
        double[,] normalMatrix = new double[coefficientCount, coefficientCount];
        double[] rhs = new double[coefficientCount];

        foreach (PositionSample sample in samples)
        {
            int[,] board = sample.CloneBoard();
            FeatureVector vector = featureExtractor.Extract(board, sample.PlayerToMove);
            double[] x = BuildDesignVector(vector, featureNames);
            double y = sample.Evaluation;

            for (int i = 0; i < coefficientCount; i++)
            {
                rhs[i] += x[i] * y;
                for (int j = 0; j < coefficientCount; j++)
                {
                    normalMatrix[i, j] += x[i] * x[j];
                }
            }
        }

        double lambda = Math.Max(0.0, options.L2Regularization);
        for (int i = 1; i < coefficientCount; i++)
        {
            normalMatrix[i, i] += lambda;
        }

        double[] coefficients = SolveLinearSystem(normalMatrix, rhs);
        Dictionary<string, double> weights = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < featureNames.Length; i++)
        {
            weights[featureNames[i]] = coefficients[i + 1];
        }

        LinearEvaluationModel model = new LinearEvaluationModel
        {
            Name = string.IsNullOrWhiteSpace(options.ModelName) ? "learned-linear-evaluator" : options.ModelName,
            TrainingSource = options.TrainingSource ?? dataset.Name,
            TrainingSampleCount = samples.Count,
            Bias = coefficients[0],
            Weights = weights
        };

        (double mae, double rmse) = ComputeErrors(samples, model);
        return new LinearEvaluationTrainingResult
        {
            Model = model,
            SampleCount = samples.Count,
            MeanAbsoluteError = mae,
            RootMeanSquaredError = rmse
        };
    }

    private (double mae, double rmse) ComputeErrors(IEnumerable<PositionSample> samples, LinearEvaluationModel model)
    {
        double absolute = 0.0;
        double squared = 0.0;
        int count = 0;

        foreach (PositionSample sample in samples)
        {
            FeatureVector features = featureExtractor.Extract(sample.CloneBoard(), sample.PlayerToMove);
            double error = model.Evaluate(features) - sample.Evaluation;
            absolute += Math.Abs(error);
            squared += error * error;
            count++;
        }

        if (count == 0)
        {
            return (0.0, 0.0);
        }

        return (absolute / count, Math.Sqrt(squared / count));
    }

    private static double[] BuildDesignVector(FeatureVector vector, string[] featureNames)
    {
        double[] values = new double[featureNames.Length + 1];
        values[0] = 1.0;
        for (int i = 0; i < featureNames.Length; i++)
        {
            values[i + 1] = vector[featureNames[i]];
        }

        return values;
    }

    private static double[] SolveLinearSystem(double[,] matrix, double[] rhs)
    {
        int n = rhs.Length;
        double[,] a = new double[n, n + 1];
        for (int row = 0; row < n; row++)
        {
            for (int column = 0; column < n; column++)
            {
                a[row, column] = matrix[row, column];
            }

            a[row, n] = rhs[row];
        }

        for (int pivot = 0; pivot < n; pivot++)
        {
            int bestRow = pivot;
            double bestValue = Math.Abs(a[pivot, pivot]);
            for (int row = pivot + 1; row < n; row++)
            {
                double value = Math.Abs(a[row, pivot]);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestRow = row;
                }
            }

            if (bestValue < 1e-9)
            {
                a[pivot, pivot] += 1e-6;
                bestValue = Math.Abs(a[pivot, pivot]);
            }

            if (bestRow != pivot)
            {
                for (int column = pivot; column <= n; column++)
                {
                    (a[pivot, column], a[bestRow, column]) = (a[bestRow, column], a[pivot, column]);
                }
            }

            double divisor = a[pivot, pivot];
            if (Math.Abs(divisor) < 1e-12)
            {
                divisor = divisor < 0 ? -1e-12 : 1e-12;
            }

            for (int column = pivot; column <= n; column++)
            {
                a[pivot, column] /= divisor;
            }

            for (int row = 0; row < n; row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                double factor = a[row, pivot];
                if (Math.Abs(factor) < 1e-12)
                {
                    continue;
                }

                for (int column = pivot; column <= n; column++)
                {
                    a[row, column] -= factor * a[pivot, column];
                }
            }
        }

        double[] solution = new double[n];
        for (int row = 0; row < n; row++)
        {
            solution[row] = a[row, n];
        }

        return solution;
    }
}
