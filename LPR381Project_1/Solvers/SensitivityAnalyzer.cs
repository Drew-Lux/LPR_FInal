using System;
using System.Collections.Generic;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    public class SensitivityAnalyzer
    {
        private LinearModel _originalModel;
        private double[,] _optimalTableau;
        private int _numVariables;
        private int _numConstraints;
        private List<int> _basicVariables;
        private List<int> _nonBasicVariables;

        public SensitivityAnalyzer(LinearModel originalModel, double[,] optimalTableau, List<int> basicVariables, List<int> nonBasicVariables)
        {
            _originalModel = originalModel;
            _optimalTableau = optimalTableau;
            _numVariables = originalModel.ObjectiveCoefficients.Count;
            _numConstraints = originalModel.Constraints.Count;
            _basicVariables = basicVariables;
            _nonBasicVariables = nonBasicVariables;
        }

        // --- HELPER: Extract Inverse Matrix (B^-1) ---
        private double[,] GetInverseMatrix()
        {
            double[,] bInverse = new double[_numConstraints, _numConstraints];
            int slackStartIndex = _numVariables + 1; // +1 to skip the Z column

            for (int i = 0; i < _numConstraints; i++)
            {
                for (int j = 0; j < _numConstraints; j++)
                {
                    bInverse[i, j] = _optimalTableau[i + 1, slackStartIndex + j]; // i+1 to skip Z row
                }
            }
            return bInverse;
        }

        // --- OPTION 1: NBV Range ---
        public void DisplayNbvRange(int varIndex)
        {
            if (_basicVariables.Contains(varIndex))
            {
                Console.WriteLine($"Error: x{varIndex + 1} is a Basic Variable, not an NBV.");
                return;
            }

            int colIndex = varIndex + 1;
            double reducedCost = Math.Abs(_optimalTableau[0, colIndex]);
            double originalCoef = _originalModel.ObjectiveCoefficients[varIndex];

            Console.WriteLine($"\n--- Range of Optimality for NBV x{varIndex + 1} ---");
            Console.WriteLine($"Original Coefficient (Cj): {originalCoef}");

            if (_originalModel.OptimizationType == "max")
            {
                Console.WriteLine($"Allowable Increase: {reducedCost:F3}");
                Console.WriteLine($"Allowable Decrease: Infinity");
                Console.WriteLine($"Range: -Infinity <= Cj <= {(originalCoef + reducedCost):F3}");
            }
            else
            {
                Console.WriteLine($"Allowable Increase: Infinity");
                Console.WriteLine($"Allowable Decrease: {reducedCost:F3}");
                Console.WriteLine($"Range: {(originalCoef - reducedCost):F3} <= Cj <= Infinity");
            }
        }

        // --- OPTIONS 2 & 4: Apply Change to Coefficient ---
        public LinearModel ApplyCoefficientChange(int varIndex, double newCoefficient)
        {
            Console.WriteLine($"\nApplying change: x{varIndex + 1} coefficient set to {newCoefficient}");
            LinearModel updatedModel = CloneModel(_originalModel);
            updatedModel.ObjectiveCoefficients[varIndex] = newCoefficient;
            return updatedModel;
        }

        // --- OPTION 3: BV Range ---
        public void DisplayBvRange(int varIndex)
        {
            if (!_basicVariables.Contains(varIndex))
            {
                Console.WriteLine($"Error: x{varIndex + 1} is an NBV, not a Basic Variable.");
                return;
            }

            int rowIndex = _basicVariables.IndexOf(varIndex) + 1;
            double originalCoef = _originalModel.ObjectiveCoefficients[varIndex];

            double maxNegativeRatio = double.MinValue;
            double minPositiveRatio = double.MaxValue;

            foreach (int nbvIndex in _nonBasicVariables)
            {
                int colIndex = nbvIndex + 1;
                double zRowVal = _optimalTableau[0, colIndex];
                double rowVal = _optimalTableau[rowIndex, colIndex];

                if (Math.Abs(rowVal) > 0.0001)
                {
                    double ratio = -(zRowVal) / rowVal;

                    if (rowVal > 0 && ratio < minPositiveRatio)
                        minPositiveRatio = ratio;
                    else if (rowVal < 0 && ratio > maxNegativeRatio)
                        maxNegativeRatio = ratio;
                }
            }

            Console.WriteLine($"\n--- Range of Optimality for BV x{varIndex + 1} ---");
            Console.WriteLine($"Original Coefficient (Cj): {originalCoef}");

            double lowerBound = maxNegativeRatio == double.MinValue ? double.NegativeInfinity : originalCoef + maxNegativeRatio;
            double upperBound = minPositiveRatio == double.MaxValue ? double.PositiveInfinity : originalCoef + minPositiveRatio;

            Console.WriteLine($"Allowable Increase (Delta): {(minPositiveRatio == double.MaxValue ? "Infinity" : minPositiveRatio.ToString("F3"))}");
            Console.WriteLine($"Allowable Decrease (Delta): {(maxNegativeRatio == double.MinValue ? "Infinity" : Math.Abs(maxNegativeRatio).ToString("F3"))}");

            string lowerStr = lowerBound == double.NegativeInfinity ? "-Infinity" : lowerBound.ToString("F3");
            string upperStr = upperBound == double.PositiveInfinity ? "Infinity" : upperBound.ToString("F3");

            Console.WriteLine($"Range: {lowerStr} <= Cj <= {upperStr}");
        }

        // --- OPTION 5: RHS Range (Feasibility) ---
        public void DisplayRhsRange(int constraintIndex)
        {
            if (constraintIndex < 0 || constraintIndex >= _numConstraints)
            {
                Console.WriteLine("Invalid constraint index.");
                return;
            }

            double[,] bInverse = GetInverseMatrix();
            double originalRhs = _originalModel.Constraints[constraintIndex].RHS;

            double maxNegativeDelta = double.MinValue;
            double minPositiveDelta = double.MaxValue;

            for (int i = 0; i < _numConstraints; i++)
            {
                double currentOptimalRhs = _optimalTableau[i + 1, _optimalTableau.GetLength(1) - 1];
                double bInverseValue = bInverse[i, constraintIndex];

                if (Math.Abs(bInverseValue) > 0.0001)
                {
                    double ratio = -(currentOptimalRhs) / bInverseValue;

                    if (bInverseValue > 0 && ratio > maxNegativeDelta)
                        maxNegativeDelta = ratio;
                    else if (bInverseValue < 0 && ratio < minPositiveDelta)
                        minPositiveDelta = ratio;
                }
            }

            Console.WriteLine($"\n--- Range of Feasibility for Constraint {constraintIndex + 1} RHS ---");
            Console.WriteLine($"Original RHS (b_i): {originalRhs}");

            double lowerBound = maxNegativeDelta == double.MinValue ? double.NegativeInfinity : originalRhs + maxNegativeDelta;
            double upperBound = minPositiveDelta == double.MaxValue ? double.PositiveInfinity : originalRhs + minPositiveDelta;

            Console.WriteLine($"Allowable Increase (Delta): {(minPositiveDelta == double.MaxValue ? "Infinity" : minPositiveDelta.ToString("F3"))}");
            Console.WriteLine($"Allowable Decrease (Delta): {(maxNegativeDelta == double.MinValue ? "Infinity" : Math.Abs(maxNegativeDelta).ToString("F3"))}");

            string lowerStr = lowerBound == double.NegativeInfinity ? "-Infinity" : lowerBound.ToString("F3");
            string upperStr = upperBound == double.PositiveInfinity ? "Infinity" : upperBound.ToString("F3");

            Console.WriteLine($"Range: {lowerStr} <= RHS <= {upperStr}");
        }

        // --- OPTION 6: Apply Change to RHS ---
        public LinearModel ApplyRhsChange(int constraintIndex, double newRhs)
        {
            Console.WriteLine($"\nApplying change: Constraint {constraintIndex + 1} RHS set to {newRhs}");
            LinearModel updatedModel = CloneModel(_originalModel);
            updatedModel.Constraints[constraintIndex].RHS = newRhs;
            return updatedModel;
        }

        // --- OPTION 7: NBV Column Range ---
        public void DisplayNbvColumnRange(int varIndex)
        {
            Console.WriteLine($"\n--- Column Range for NBV x{varIndex + 1} ---");
            Console.WriteLine("To maintain optimality, the new technological coefficients (a_new) must satisfy:");
            Console.WriteLine("Cj - Sum(ShadowPrice_i * a_new_i) <= 0");
            Console.WriteLine("Use Option 11 to view Shadow Prices for substitution.");
        }

        // --- OPTION 8: Apply Change in NBV Column ---
        public LinearModel ApplyColumnChange(int varIndex, List<double> newColumn)
        {
            if (newColumn.Count != _numConstraints)
            {
                throw new Exception("New column must have the same number of entries as constraints.");
            }

            Console.WriteLine($"\nApplying structural change to column x{varIndex + 1}...");
            LinearModel updatedModel = CloneModel(_originalModel);

            for (int i = 0; i < _numConstraints; i++)
            {
                updatedModel.Constraints[i].Coefficients[varIndex] = newColumn[i];
            }
            return updatedModel;
        }

        // --- OPTION 9: Add New Activity (Variable) ---
        public LinearModel AddNewActivity(double objCoefficient, List<double> constraintCoefficients, string signRestriction)
        {
            if (constraintCoefficients.Count != _numConstraints)
            {
                throw new Exception("Constraint coefficients must match the number of existing constraints.");
            }

            Console.WriteLine($"\nAdding new activity (x{_numVariables + 1})...");
            LinearModel updatedModel = CloneModel(_originalModel);

            updatedModel.ObjectiveCoefficients.Add(objCoefficient);
            updatedModel.SignRestrictions.Add(signRestriction);

            for (int i = 0; i < _numConstraints; i++)
            {
                updatedModel.Constraints[i].Coefficients.Add(constraintCoefficients[i]);
            }
            return updatedModel;
        }

        // --- OPTION 10: Add New Constraint ---
        public LinearModel AddNewConstraint(Constraint newConstraint)
        {
            if (newConstraint.Coefficients.Count != _numVariables)
            {
                throw new Exception("New constraint must have coefficients for all existing decision variables.");
            }

            Console.WriteLine("\nAdding new constraint...");
            LinearModel updatedModel = CloneModel(_originalModel);
            updatedModel.Constraints.Add(newConstraint);
            return updatedModel;
        }

        // --- OPTION 11: Display Shadow Prices ---
        public void DisplayShadowPrices()
        {
            Console.WriteLine("\n--- SHADOW PRICES (DUAL VARIABLES) ---");
            int slackStartIndex = _numVariables + 1;

            for (int i = 0; i < _numConstraints; i++)
            {
                // Shadow prices are located in the Z-row under the slack variables
                double shadowPrice = Math.Abs(_optimalTableau[0, slackStartIndex + i]);
                Console.WriteLine($"Constraint {i + 1} Shadow Price (y{i + 1}): {shadowPrice:F3}");
            }
            Console.WriteLine("\nNote: The shadow price represents the marginal value of increasing the constraint's RHS by 1 unit.");
        }

        // --- HELPER: Deep Clone Model ---
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
    }
}