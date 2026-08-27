using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    public class RevisedPrimalSimplexSolver : ISolver
    {
        public SimplexResult Solve(LinearModel model, string outputFilePath)
        {
            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                writer.WriteLine("--- REVISED PRIMAL SIMPLEX ALGORITHM ---");

                int numConstraints = model.Constraints.Count;
                int numDecisionVars = model.ObjectiveCoefficients.Count;
                int totalVars = numDecisionVars + numConstraints; // Simplified for <= constraints (adding slacks)

                // Initialize B inverse as an Identity Matrix
                double[,] bInverse = new double[numConstraints, numConstraints];
                for (int i = 0; i < numConstraints; i++) bInverse[i, i] = 1.0;

                // Track indices of Basic (BV) and Non-Basic Variables (NBV)
                List<int> basicVars = new List<int>();
                List<int> nonBasicVars = new List<int>();

                for (int i = 0; i < numDecisionVars; i++) nonBasicVars.Add(i);
                for (int i = 0; i < numConstraints; i++) basicVars.Add(numDecisionVars + i); // Slacks start as basic

                // Objective coefficients for all variables (decision + slacks)
                double[] allObjCoefs = new double[totalVars];
                for (int i = 0; i < numDecisionVars; i++)
                {
                    allObjCoefs[i] = model.OptimizationType == "max" ? model.ObjectiveCoefficients[i] : -model.ObjectiveCoefficients[i];
                }

                // Initial Right Hand Side (RHS)
                double[] xb = new double[numConstraints];
                for (int i = 0; i < numConstraints; i++) xb[i] = model.Constraints[i].RHS;

                int iteration = 0;
                bool optimal = false;

                while (!optimal)
                {
                    writer.WriteLine($"\n--- Iteration {iteration} ---");

                    // 1. PRICE OUT: Calculate reduced costs (zj - cj) for Non-Basic Variables
                    writer.WriteLine("Price Out Iteration:");
                    double[] cb = new double[numConstraints];
                    for (int i = 0; i < numConstraints; i++) cb[i] = allObjCoefs[basicVars[i]];

                    // Calculate Simplex Multipliers (y = Cb * B^-1)
                    double[] y = MultiplyVectorByMatrix(cb, bInverse);

                    int enteringVarIndex = -1;
                    int enteringVarGlobalIndex = -1;
                    double bestReducedCost = 0; // For maximization, we look for most negative reduced cost

                    for (int j = 0; j < nonBasicVars.Count; j++)
                    {
                        int varIndex = nonBasicVars[j];
                        double[] columnA = GetOriginalColumn(model, varIndex, numConstraints);

                        double zj = 0;
                        for (int i = 0; i < numConstraints; i++) zj += y[i] * columnA[i];

                        double reducedCost = allObjCoefs[varIndex] - zj; // cj - zj

                        writer.WriteLine($"Reduced cost for x{varIndex + 1}: {Math.Round(reducedCost, 3).ToString("F3")}");

                        if (reducedCost > bestReducedCost) // Assuming standard max logic translated
                        {
                            bestReducedCost = reducedCost;
                            enteringVarIndex = j;
                            enteringVarGlobalIndex = varIndex;
                        }
                    }

                    if (bestReducedCost <= 0.0001) // Tolerance for floating point
                    {
                        optimal = true;
                        writer.WriteLine("\nOptimal Solution Reached.");
                        break;
                    }

                    writer.WriteLine($"Entering Variable: x{enteringVarGlobalIndex + 1}");

                    // 2. PRODUCT FORM: Calculate entering column in current basis (d = B^-1 * a)
                    writer.WriteLine("\nProduct Form Iteration:");
                    double[] aCol = GetOriginalColumn(model, enteringVarGlobalIndex, numConstraints);
                    double[] d = MultiplyMatrixByVector(bInverse, aCol);

                    for (int i = 0; i < d.Length; i++)
                    {
                        writer.WriteLine($"d{i + 1}: {Math.Round(d[i], 3).ToString("F3")}");
                    }

                    // 3. RATIO TEST: Find Leaving Variable
                    int leavingVarIndex = -1;
                    double minRatio = double.MaxValue;

                    for (int i = 0; i < numConstraints; i++)
                    {
                        if (d[i] > 0) // Only positive coefficients are valid for ratio test
                        {
                            double ratio = xb[i] / d[i];
                            if (ratio < minRatio)
                            {
                                minRatio = ratio;
                                leavingVarIndex = i;
                            }
                        }
                    }

                    // Handle unboundness special case requirement
                    if (leavingVarIndex == -1)
                    {
                        writer.WriteLine("\nError: The model is unbounded.");
                        return null;
                    }

                    writer.WriteLine($"Leaving Variable (Row {leavingVarIndex + 1}): x{basicVars[leavingVarIndex] + 1}");

                    // 4. UPDATE B INVERSE (Eta Matrix operations)
                    double pivot = d[leavingVarIndex];
                    double[,] eta = new double[numConstraints, numConstraints];

                    for (int i = 0; i < numConstraints; i++) eta[i, i] = 1.0;
                    for (int i = 0; i < numConstraints; i++)
                    {
                        if (i == leavingVarIndex)
                            eta[i, leavingVarIndex] = 1.0 / pivot;
                        else
                            eta[i, leavingVarIndex] = -d[i] / pivot;
                    }

                    bInverse = MultiplyMatrices(eta, bInverse);

                    // Update RHS (xb)
                    xb = MultiplyMatrixByVector(eta, xb);

                    // Swap Basic and Non-Basic variables
                    int temp = basicVars[leavingVarIndex];
                    basicVars[leavingVarIndex] = nonBasicVars[enteringVarIndex];
                    nonBasicVars[enteringVarIndex] = temp;

                    iteration++;
                }

                // Final Output
                writer.WriteLine("\n--- FINAL OPTIMAL SOLUTION ---");
                double zTotal = 0;
                for (int i = 0; i < numConstraints; i++)
                {
                    writer.WriteLine($"x{basicVars[i] + 1} = {Math.Round(xb[i], 3).ToString("F3")}");
                    zTotal += allObjCoefs[basicVars[i]] * xb[i];
                }
                writer.WriteLine($"Optimal Z = {Math.Round(zTotal, 3).ToString("F3")}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nRevised Simplex optimization complete. Results saved to {outputFilePath}");
            Console.ResetColor();

            return null;
        }

        // --- Helper Methods for Matrix Operations ---

        private double[] GetOriginalColumn(LinearModel model, int varIndex, int numConstraints)
        {
            double[] col = new double[numConstraints];
            int numDecisionVars = model.ObjectiveCoefficients.Count;

            for (int i = 0; i < numConstraints; i++)
            {
                if (varIndex < numDecisionVars)
                    col[i] = model.Constraints[i].Coefficients[varIndex];
                else
                    col[i] = (varIndex - numDecisionVars == i) ? 1.0 : 0.0; // Slack variable columns
            }
            return col;
        }

        private double[] MultiplyVectorByMatrix(double[] v, double[,] m)
        {
            int size = v.Length;
            double[] result = new double[size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++) result[j] += v[i] * m[i, j];
            }
            return result;
        }

        private double[] MultiplyMatrixByVector(double[,] m, double[] v)
        {
            int size = v.Length;
            double[] result = new double[size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++) result[i] += m[i, j] * v[j];
            }
            return result;
        }

        private double[,] MultiplyMatrices(double[,] m1, double[,] m2)
        {
            int size = m1.GetLength(0);
            double[,] result = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    for (int k = 0; k < size; k++) result[i, j] += m1[i, k] * m2[k, j];
                }
            }
            return result;
        }
    }
}