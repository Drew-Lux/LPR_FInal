using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    public class CuttingPlaneSolver : ISolver
    {
        public SimplexResult Solve(LinearModel rootModel, string outputFilePath)
        {
            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                writer.WriteLine("=========================================");
                writer.WriteLine("        CUTTING PLANE ALGORITHM          ");
                writer.WriteLine("=========================================\n");

                bool isIntegerOptimal = false;
                int iteration = 1;

                // Clone the root model so we can append cuts dynamically without corrupting the original
                LinearModel currentModel = CloneModel(rootModel);

                while (!isIntegerOptimal)
                {
                    if (iteration > 500)
                    {
                        writer.WriteLine("\nCutting Plane did not converge within the iteration limit.");
                        break;
                    }

                    writer.WriteLine($"\n--- CUTTING PLANE ITERATION {iteration} ---");

                    // 1. Solve the current continuous model
                    var result = SolveContinuousLP(currentModel, writer);

                    if (!result.IsFeasible)
                    {
                        writer.WriteLine("The model has no feasible integer solution.");
                        break;
                    }

                    writer.WriteLine($"Relaxation Z = {result.Z:F3}");
                    for (int i = 0; i < result.Variables.Length; i++)
                    {
                        writer.WriteLine($"x{i + 1} = {result.Variables[i]:F3}");
                    }

                    // 2. Integrality Check
                    int fractionalVarRowIndex = GetVariableWithLargestFraction(result.Variables, currentModel.SignRestrictions);

                    if (fractionalVarRowIndex == -1)
                    {
                        isIntegerOptimal = true;
                        writer.WriteLine("\n*** Optimal Integer Solution Found ***");

                        writer.WriteLine($"Optimal Z = {result.Z:F3}");
                        for (int i = 0; i < result.Variables.Length; i++)
                        {
                            writer.WriteLine($"x{i + 1} = {result.Variables[i]:F3}");
                        }
                        break;
                    }

                    // 3. Generate Gomory Cut
                    // The fractional part of a number 'v' is: f = v - floor(v)
                    writer.WriteLine($"\nFractional variable detected. Generating Gomory Cut for variable index {fractionalVarRowIndex + 1}...");

                    Constraint gomoryCut = BuildGomoryCut(result, fractionalVarRowIndex);

                    writer.WriteLine($"Cut Added: Sum(f_j * x_j) >= {gomoryCut.RHS:F3}");

                    // 4. Append Cut to the Model (expressed purely in the original decision variables)
                    currentModel.Constraints.Add(gomoryCut);

                    iteration++;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nCutting Plane optimization complete. Results saved to {outputFilePath}");
            Console.ResetColor();

            return null;
        }

        // --- Helper Methods ---

        private int GetVariableWithLargestFraction(double[] variables, List<string> restrictions)
        {
            double maxFraction = 0;
            int targetIndex = -1;
            double tolerance = 0.00001;

            for (int i = 0; i < variables.Length; i++)
            {
                if (restrictions[i] == "int" || restrictions[i] == "bin")
                {
                    double fraction = variables[i] - Math.Floor(variables[i]);

                    // We want a fractional part strictly between 0 and 1 (ignoring rounding errors)
                    if (fraction > tolerance && fraction < 1 - tolerance)
                    {
                        if (fraction > maxFraction)
                        {
                            maxFraction = fraction;
                            targetIndex = i;
                        }
                    }
                }
            }
            return targetIndex;
        }

        private LinearModel CloneModel(LinearModel original)
        {
            LinearModel newModel = new LinearModel
            {
                OptimizationType = original.OptimizationType,
                ObjectiveCoefficients = new List<double>(original.ObjectiveCoefficients),
                SignRestrictions = new List<string>(original.SignRestrictions),
                Constraints = new List<Constraint>()
            };

            foreach (var c in original.Constraints)
            {
                newModel.Constraints.Add(new Constraint
                {
                    Coefficients = new List<double>(c.Coefficients),
                    Relation = c.Relation,
                    RHS = c.RHS
                });
            }
            return newModel;
        }

        private LpRelaxationResult SolveContinuousLP(LinearModel model, StreamWriter writer)
        {
            return LpRelaxationSolver.Solve(model);
        }

        // Builds a Gomory fractional cut for the basic variable at decision-variable index
        // fractionalVarIndex, rewriting any slack/surplus terms back into the original
        // decision variables so the cut can be appended to the model as a plain constraint.
        private Constraint BuildGomoryCut(LpRelaxationResult result, int fractionalVarIndex)
        {
            int n = result.NumDecisionVars;
            int cols = result.TotalVars + 2;
            int row = result.BasicVariables.IndexOf(fractionalVarIndex) + 1;

            double[] cutCoefficients = new double[n];
            double constantAdjustment = 0.0;

            foreach (int col in result.NonBasicVariables)
            {
                double value = result.Tableau[row, col + 1];
                double fraction = value - Math.Floor(value);
                if (Math.Abs(fraction) < 1e-9) continue;

                if (col < n)
                {
                    cutCoefficients[col] += fraction;
                }
                else
                {
                    double[] def = result.ExtraCoefficients[col];
                    double constant = result.ExtraConstant[col];
                    for (int j = 0; j < n; j++) cutCoefficients[j] += fraction * def[j];
                    constantAdjustment += fraction * constant;
                }
            }

            double rhsValue = result.Tableau[row, cols - 1];
            double rhsFraction = rhsValue - Math.Floor(rhsValue);

            return new Constraint
            {
                Relation = ">=",
                Coefficients = new List<double>(cutCoefficients),
                RHS = Math.Round(rhsFraction - constantAdjustment, 5)
            };
        }
    }
}
