using System;
using System.Collections.Generic;
using System.IO;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    public class PrimalSimplexSolver : ISolver
    {
        private double[,] _tableau;
        private int _rows;
        private int _cols;

        // Track variable indices
        private List<int> _basicVariables;
        private List<int> _nonBasicVariables;

        public SimplexResult Solve(LinearModel model, string outputFilePath)
        {
            bool optimal = false;

            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                writer.WriteLine("--- PRIMAL SIMPLEX ALGORITHM ---");
                InitializeTableau(model);

                int iteration = 0;

                while (!optimal)
                {
                    writer.WriteLine($"\nIteration {iteration}:");
                    WriteTableau(writer);

                    int enteringCol = GetEnteringVariable(model.OptimizationType);

                    if (enteringCol == -1)
                    {
                        optimal = true;
                        writer.WriteLine("\nOptimal Solution Reached.");
                        break;
                    }

                    int leavingRow = GetLeavingVariable(enteringCol);

                    if (leavingRow == -1)
                    {
                        writer.WriteLine("\nError: The model is unbounded.");
                        break;
                    }

                    Pivot(enteringCol, leavingRow);
                    iteration++;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nOptimization complete. Results saved to {outputFilePath}");
            Console.ResetColor();

            // Return the final state for Sensitivity Analysis
            return new SimplexResult
            {
                IsOptimal = optimal,
                FinalTableau = _tableau,
                BasicVariables = _basicVariables,
                NonBasicVariables = _nonBasicVariables
            };
        }

        private void InitializeTableau(LinearModel model)
        {
            int decisionVars = model.ObjectiveCoefficients.Count;
            int slacks = model.Constraints.Count;

            _rows = model.Constraints.Count + 1;
            _cols = decisionVars + slacks + 2;

            _tableau = new double[_rows, _cols];
            _basicVariables = new List<int>();
            _nonBasicVariables = new List<int>();

            // Initialize variable tracking
            for (int i = 0; i < decisionVars; i++) _nonBasicVariables.Add(i);
            for (int i = 0; i < slacks; i++) _basicVariables.Add(decisionVars + i);

            // Setup Z Row (Row 0)
            _tableau[0, 0] = 1;
            for (int j = 0; j < decisionVars; j++)
            {
                _tableau[0, j + 1] = model.OptimizationType == "max" ? -model.ObjectiveCoefficients[j] : model.ObjectiveCoefficients[j];
            }

            // Setup Constraint Rows
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                var constraint = model.Constraints[i];
                for (int j = 0; j < constraint.Coefficients.Count; j++)
                {
                    _tableau[i + 1, j + 1] = constraint.Coefficients[j];
                }

                _tableau[i + 1, decisionVars + 1 + i] = 1;
                _tableau[i + 1, _cols - 1] = constraint.RHS;
            }
        }

        private int GetEnteringVariable(string optType)
        {
            int bestCol = -1;
            double bestVal = 0;

            for (int j = 1; j < _cols - 1; j++)
            {
                if (optType == "max" && _tableau[0, j] < bestVal)
                {
                    bestVal = _tableau[0, j];
                    bestCol = j;
                }
                else if (optType == "min" && _tableau[0, j] > bestVal)
                {
                    bestVal = _tableau[0, j];
                    bestCol = j;
                }
            }
            return bestCol;
        }

        private int GetLeavingVariable(int enteringCol)
        {
            int bestRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < _rows; i++)
            {
                double coefficient = _tableau[i, enteringCol];
                if (coefficient > 0)
                {
                    double ratio = _tableau[i, _cols - 1] / coefficient;
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        bestRow = i;
                    }
                }
            }
            return bestRow;
        }

        private void Pivot(int pivotCol, int pivotRow)
        {
            double pivotElement = _tableau[pivotRow, pivotCol];

            for (int j = 0; j < _cols; j++)
            {
                _tableau[pivotRow, j] /= pivotElement;
            }

            for (int i = 0; i < _rows; i++)
            {
                if (i != pivotRow)
                {
                    double factor = _tableau[i, pivotCol];
                    for (int j = 0; j < _cols; j++)
                    {
                        _tableau[i, j] -= factor * _tableau[pivotRow, j];
                    }
                }
            }

            // Update variable tracking logic
            int enteringVar = pivotCol - 1;
            int leavingVarIndex = pivotRow - 1;

            int leavingVar = _basicVariables[leavingVarIndex];

            _basicVariables[leavingVarIndex] = enteringVar;
            _nonBasicVariables.Remove(enteringVar);
            _nonBasicVariables.Add(leavingVar);
        }

        private void WriteTableau(StreamWriter writer)
        {
            for (int i = 0; i < _rows; i++)
            {
                for (int j = 0; j < _cols; j++)
                {
                    writer.Write(Math.Round(_tableau[i, j], 3).ToString("F3").PadRight(10));
                }
                writer.WriteLine();
            }
        }
    }
}