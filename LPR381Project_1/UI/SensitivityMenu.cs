using System;
using System.Collections.Generic;
using LPR381_Project.Models;
using LPR381_Project.Solvers;

namespace LPR381_Project.UI
{
    public class SensitivityMenu
    {
        private LinearModel _solvedModel;
        private SensitivityAnalyzer _analyzer;
        private Action<LinearModel> _updateModelCallback;

        public SensitivityMenu(LinearModel solvedModel, double[,] optimalTableau, List<int> basicVariables, List<int> nonBasicVariables, Action<LinearModel> updateCallback)
        {
            _solvedModel = solvedModel;
            _analyzer = new SensitivityAnalyzer(solvedModel, optimalTableau, basicVariables, nonBasicVariables);
            _updateModelCallback = updateCallback;
        }

        public void Run()
        {
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("=========================================");
                Console.WriteLine("         SENSITIVITY ANALYSIS            ");
                Console.WriteLine("=========================================");
                Console.WriteLine("1. Display range of a Non-Basic Variable (NBV)");
                Console.WriteLine("2. Apply change to a Non-Basic Variable");
                Console.WriteLine("3. Display range of a Basic Variable (BV)");
                Console.WriteLine("4. Apply change to a Basic Variable");
                Console.WriteLine("5. Display range of a constraint RHS");
                Console.WriteLine("6. Apply change to a constraint RHS");
                Console.WriteLine("7. Display range in an NBV column");
                Console.WriteLine("8. Apply change in an NBV column");
                Console.WriteLine("9. Add a new activity (Variable)");
                Console.WriteLine("10. Add a new constraint");
                Console.WriteLine("11. Display Shadow Prices");
                Console.WriteLine("12. Apply and Solve Duality");
                Console.WriteLine("13. Return to Main Menu");
                Console.WriteLine("=========================================");
                Console.Write("Select an option: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        Console.Write("Enter the zero-based index of the Non-Basic Variable (e.g., 0 for x1): ");
                        if (int.TryParse(Console.ReadLine(), out int nbvIndex))
                        {
                            _analyzer.DisplayNbvRange(nbvIndex);
                        }
                        WaitForKey();
                        break;

                    case "2":
                    case "4":
                        Console.Write("Enter the zero-based index of the variable to change: ");
                        if (int.TryParse(Console.ReadLine(), out int varIndex))
                        {
                            Console.Write("Enter the new objective coefficient: ");
                            if (double.TryParse(Console.ReadLine(), out double newCoef))
                            {
                                LinearModel updatedModel = _analyzer.ApplyCoefficientChange(varIndex, newCoef);
                                _updateModelCallback(updatedModel);
                                Console.WriteLine("\nModel updated. You can now solve this modified model from the main menu.");
                            }
                        }
                        WaitForKey();
                        break;

                    case "3":
                        Console.Write("Enter the zero-based index of the Basic Variable (e.g., 1 for x2): ");
                        if (int.TryParse(Console.ReadLine(), out int bvIndex))
                        {
                            _analyzer.DisplayBvRange(bvIndex);
                        }
                        WaitForKey();
                        break;

                    case "5":
                        Console.Write($"Enter the zero-based index of the Constraint (0 to {_solvedModel.Constraints.Count - 1}): ");
                        if (int.TryParse(Console.ReadLine(), out int constraintIndex))
                        {
                            _analyzer.DisplayRhsRange(constraintIndex);
                        }
                        WaitForKey();
                        break;

                    case "6":
                        Console.Write($"Enter the zero-based index of the Constraint (0 to {_solvedModel.Constraints.Count - 1}): ");
                        if (int.TryParse(Console.ReadLine(), out int changeConstraintIndex))
                        {
                            Console.Write("Enter the new RHS value: ");
                            if (double.TryParse(Console.ReadLine(), out double newRhs))
                            {
                                LinearModel updatedModel = _analyzer.ApplyRhsChange(changeConstraintIndex, newRhs);
                                _updateModelCallback(updatedModel);
                                Console.WriteLine("\nRHS updated. You can now solve this modified model from the main menu.");
                            }
                        }
                        WaitForKey();
                        break;

                    case "7":
                        Console.Write("Enter the zero-based index of the Non-Basic Variable column: ");
                        if (int.TryParse(Console.ReadLine(), out int colIndex))
                        {
                            _analyzer.DisplayNbvColumnRange(colIndex);
                        }
                        WaitForKey();
                        break;

                    case "8":
                        Console.Write("Enter the zero-based index of the Non-Basic Variable column to change: ");
                        if (int.TryParse(Console.ReadLine(), out int changeColIndex))
                        {
                            List<double> newCol = new List<double>();
                            for (int i = 0; i < _solvedModel.Constraints.Count; i++)
                            {
                                Console.Write($"Enter new coefficient for constraint {i + 1}: ");
                                if (double.TryParse(Console.ReadLine(), out double val))
                                {
                                    newCol.Add(val);
                                }
                            }
                            LinearModel updatedColModel = _analyzer.ApplyColumnChange(changeColIndex, newCol);
                            _updateModelCallback(updatedColModel);
                            Console.WriteLine("\nColumn updated. You can now solve this modified model from the main menu.");
                        }
                        WaitForKey();
                        break;

                    case "9":
                        Console.Write("Enter new objective coefficient: ");
                        if (double.TryParse(Console.ReadLine(), out double objCoef))
                        {
                            List<double> newConstraintCoefs = new List<double>();
                            for (int i = 0; i < _solvedModel.Constraints.Count; i++)
                            {
                                Console.Write($"Enter coefficient for constraint {i + 1}: ");
                                if (double.TryParse(Console.ReadLine(), out double val))
                                {
                                    newConstraintCoefs.Add(val);
                                }
                            }
                            Console.Write("Enter sign restriction (+, -, urs, int, bin): ");
                            string sign = Console.ReadLine();

                            LinearModel updatedActivityModel = _analyzer.AddNewActivity(objCoef, newConstraintCoefs, sign);
                            _updateModelCallback(updatedActivityModel);
                            Console.WriteLine("\nNew activity added. You can solve this modified model from the main menu.");
                        }
                        WaitForKey();
                        break;

                    case "10":
                        Constraint newConstraint = new Constraint();
                        Console.WriteLine("Enter coefficients for the new constraint:");
                        for (int i = 0; i < _solvedModel.ObjectiveCoefficients.Count; i++)
                        {
                            Console.Write($"Coefficient for x{i + 1}: ");
                            if (double.TryParse(Console.ReadLine(), out double val))
                            {
                                newConstraint.Coefficients.Add(val);
                            }
                        }
                        Console.Write("Enter relation (<=, >=, =): ");
                        newConstraint.Relation = Console.ReadLine();
                        Console.Write("Enter RHS: ");
                        if (double.TryParse(Console.ReadLine(), out double cRhs))
                        {
                            newConstraint.RHS = cRhs;
                            LinearModel updatedConstraintModel = _analyzer.AddNewConstraint(newConstraint);
                            _updateModelCallback(updatedConstraintModel);
                            Console.WriteLine("\nNew constraint added. You can solve this modified model from the main menu.");
                        }
                        WaitForKey();
                        break;

                    case "11":
                        _analyzer.DisplayShadowPrices();
                        WaitForKey();
                        break;

                    case "12":
                        RunDuality();
                        break;

                    case "13":
                        exit = true;
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid selection. Please try again.");
                        Console.ResetColor();
                        WaitForKey();
                        break;
                }
            }
        }

        private void RunDuality()
        {
            Console.Clear();
            Console.WriteLine("--- DUALITY TRANSFORMATION ---");

            LinearModel dualModel = CreateDualModel(_solvedModel);
            Console.WriteLine("Dual Model successfully generated.");

            Console.Write("Enter output file path for Dual solve (e.g., dual_out.txt): ");
            string outPath = Console.ReadLine();

            try
            {
                ISolver dualSolver = new PrimalSimplexSolver();
                dualSolver.Solve(dualModel, outPath);

                Console.WriteLine("\nComparing Primal Z and Dual W...");
                Console.WriteLine("If Primal Z == Dual W, Strong Duality is verified.");
                Console.WriteLine("If Primal Z != Dual W (but bounds exist), Weak Duality applies.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError solving Dual Model: {ex.Message}");
            }

            WaitForKey();
        }

        private LinearModel CreateDualModel(LinearModel primal)
        {
            LinearModel dual = new LinearModel();

            dual.OptimizationType = primal.OptimizationType == "max" ? "min" : "max";

            foreach (var constraint in primal.Constraints)
            {
                dual.ObjectiveCoefficients.Add(constraint.RHS);
            }

            int primalVarCount = primal.ObjectiveCoefficients.Count;
            for (int i = 0; i < primalVarCount; i++)
            {
                Constraint dualConstraint = new Constraint();
                foreach (var primalConstraint in primal.Constraints)
                {
                    dualConstraint.Coefficients.Add(primalConstraint.Coefficients[i]);
                }

                dualConstraint.Relation = primal.OptimizationType == "max" ? ">=" : "<=";
                dualConstraint.RHS = primal.ObjectiveCoefficients[i];

                dual.Constraints.Add(dualConstraint);
            }
            return dual;
        }

        private void WaitForKey()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}