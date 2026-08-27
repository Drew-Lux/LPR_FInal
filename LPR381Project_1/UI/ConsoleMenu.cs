using System;
using System.Collections.Generic;
using LPR381_Project.Models;
using LPR381_Project.Parsers;
using LPR381_Project.Solvers;

namespace LPR381_Project.UI
{
    public class ConsoleMenu
    {
        private LinearModel _currentModel;
        private SimplexResult _lastOptimizationResult; // Added container state
        private readonly InputParser _parser;

        public ConsoleMenu()
        {
            _parser = new InputParser();
        }

        public void Run()
        {
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                DrawHeader("LPR381 - LINEAR & INTEGER PROGRAMMING SOLVER");

                Console.WriteLine("1. Load Input Text File");
                Console.WriteLine("2. Display Canonical Form");
                Console.WriteLine("3. Primal Simplex Algorithm");
                Console.WriteLine("4. Revised Primal Simplex Algorithm");
                Console.WriteLine("5. Branch and Bound Simplex Algorithm");
                Console.WriteLine("6. Cutting Plane Algorithm");
                Console.WriteLine("7. Sensitivity Analysis");
                Console.WriteLine("8. Branch and Bound Knapsack Algorithm");
                Console.WriteLine("9. Exit");
                Console.WriteLine(new string('-', 50));
                Console.Write("Select an option (1-9): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        LoadFile();
                        break;
                    case "2":
                        DisplayCanonicalForm();
                        break;
                    case "3":
                        RunPrimalSimplex();
                        break;
                    case "4":
                        RunRevisedPrimalSimplex();
                        break;
                    case "5":
                        RunBranchAndBound();
                        break;
                    case "6":
                        RunCuttingPlane();
                        break;
                    case "7":
                        RunSensitivityAnalysis();
                        break;
                    case "8":
                        RunKnapsackSolver();
                        break;
                    case "9":
                        exit = true;
                        break; // Fixed fall-through error
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid selection. Please try again.");
                        Console.ResetColor();
                        WaitForKey();
                        break;
                }
            }
        }

        private void LoadFile()
        {
            Console.Clear();
            DrawHeader("LOAD INPUT FILE");
            Console.Write("Enter the full path to the text file (e.g., input.txt): ");
            string filePath = Console.ReadLine();

            try
            {
                _currentModel = _parser.ParseFile(filePath);
                _lastOptimizationResult = null; // Reset optimization state on new file
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nFile successfully loaded and parsed!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError loading file: {ex.Message}");
                Console.ResetColor();
            }
            WaitForKey();
        }

        private void DisplayCanonicalForm()
        {
            Console.Clear();
            DrawHeader("CANONICAL FORM");

            if (_currentModel == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No model loaded. Please load a file first.");
                Console.ResetColor();
                WaitForKey();
                return;
            }

            int decisionVarCount = _currentModel.ObjectiveCoefficients.Count;
            List<string> headers = new List<string> { "BV" };

            for (int i = 1; i <= decisionVarCount; i++) headers.Add($"x{i}");

            int sCount = 0, eCount = 0, aCount = 0;
            List<string> constraintVars = new List<string>();

            foreach (var constraint in _currentModel.Constraints)
            {
                if (constraint.Relation == "<=") { sCount++; headers.Add($"s{sCount}"); constraintVars.Add($"s{sCount}"); }
                else if (constraint.Relation == ">=") { eCount++; aCount++; headers.Add($"e{eCount}"); headers.Add($"a{aCount}"); constraintVars.Add($"a{aCount}"); }
                else if (constraint.Relation == "=") { aCount++; headers.Add($"a{aCount}"); constraintVars.Add($"a{aCount}"); }
            }
            headers.Add("RHS");

            int colWidth = 8;

            Console.ForegroundColor = ConsoleColor.Cyan;
            foreach (var header in headers)
            {
                Console.Write(header.PadRight(colWidth));
            }
            Console.WriteLine();
            Console.ResetColor();
            Console.WriteLine(new string('-', headers.Count * colWidth));

            Console.Write("Z".PadRight(colWidth));

            foreach (var coef in _currentModel.ObjectiveCoefficients)
            {
                double printVal = _currentModel.OptimizationType == "max" ? (coef * -1) : coef;
                Console.Write(printVal.ToString("0.###").PadRight(colWidth));
            }

            int totalExtraVars = sCount + eCount + aCount;
            for (int i = 0; i < totalExtraVars; i++) Console.Write("0".PadRight(colWidth));
            Console.WriteLine("0".PadRight(colWidth));

            int currentExtraVarIndex = 0;
            for (int i = 0; i < _currentModel.Constraints.Count; i++)
            {
                var constraint = _currentModel.Constraints[i];
                Console.Write(constraintVars[i].PadRight(colWidth));

                foreach (var coef in constraint.Coefficients)
                {
                    Console.Write(coef.ToString("0.###").PadRight(colWidth));
                }

                for (int j = 0; j < totalExtraVars; j++)
                {
                    if (j == currentExtraVarIndex && constraint.Relation == "<=") { Console.Write("1".PadRight(colWidth)); currentExtraVarIndex++; }
                    else if (j == currentExtraVarIndex && constraint.Relation == ">=")
                    {
                        Console.Write("-1".PadRight(colWidth));
                        Console.Write("1".PadRight(colWidth));
                        currentExtraVarIndex += 2;
                        j++;
                    }
                    else if (j == currentExtraVarIndex && constraint.Relation == "=") { Console.Write("1".PadRight(colWidth)); currentExtraVarIndex++; }
                    else { Console.Write("0".PadRight(colWidth)); }
                }

                Console.WriteLine(constraint.RHS.ToString("0.###").PadRight(colWidth));
            }

            WaitForKey();
        }

        private void RunPrimalSimplex()
        {
            Console.Clear();
            DrawHeader("PRIMAL SIMPLEX ALGORITHM");

            if (_currentModel == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No model loaded. Please load an input file first using Option 1.");
                Console.ResetColor();
                WaitForKey();
                return;
            }

            Console.Write("Enter the full path for the output file (e.g., output.txt): ");
            string outputPath = Console.ReadLine();

            try
            {
                ISolver simplexSolver = new PrimalSimplexSolver();
                // Capture the returned result to use in sensitivity analysis!
                _lastOptimizationResult = simplexSolver.Solve(_currentModel, outputPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nCritical Error during execution: {ex.Message}");
                Console.ResetColor();
            }

            WaitForKey();
        }

        private void RunRevisedPrimalSimplex()
        {
            Console.Clear();
            DrawHeader("REVISED PRIMAL SIMPLEX ALGORITHM");

            if (_currentModel == null)
            {
                Console.WriteLine("No model loaded.");
                WaitForKey();
                return;
            }

            Console.Write("Enter output file path: ");
            string outputPath = Console.ReadLine();

            ISolver solver = new RevisedPrimalSimplexSolver();
            solver.Solve(_currentModel, outputPath);
            WaitForKey();
        }

        private void RunBranchAndBound()
        {
            Console.Clear();
            DrawHeader("BRANCH AND BOUND SIMPLEX ALGORITHM");

            if (_currentModel == null)
            {
                Console.WriteLine("No model loaded.");
                WaitForKey();
                return;
            }

            Console.Write("Enter output file path: ");
            string outputPath = Console.ReadLine();

            ISolver solver = new BranchAndBoundSolver();
            solver.Solve(_currentModel, outputPath);
            WaitForKey();
        }

        private void RunCuttingPlane()
        {
            Console.Clear();
            DrawHeader("CUTTING PLANE ALGORITHM");

            if (_currentModel == null)
            {
                Console.WriteLine("No model loaded.");
                WaitForKey();
                return;
            }

            Console.Write("Enter output file path: ");
            string outputPath = Console.ReadLine();

            ISolver solver = new CuttingPlaneSolver();
            solver.Solve(_currentModel, outputPath);
            WaitForKey();
        }

        private void RunSensitivityAnalysis()
        {
            if (_currentModel == null || _lastOptimizationResult == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please load and solve a model (Option 3) before running Sensitivity Analysis.");
                Console.ResetColor();
                WaitForKey();
                return;
            }

            if (!_lastOptimizationResult.IsOptimal)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The previous solution was not optimal or bounded. Sensitivity analysis cannot be performed.");
                Console.ResetColor();
                WaitForKey();
                return;
            }

            SensitivityMenu sensMenu = new SensitivityMenu(
                _currentModel,
                _lastOptimizationResult.FinalTableau,
                _lastOptimizationResult.BasicVariables,
                _lastOptimizationResult.NonBasicVariables
            );
            sensMenu.Run();
        }

        private void RunKnapsackSolver()
        {
            Console.Clear();
            DrawHeader("BRANCH & BOUND KNAPSACK ALGORITHM");

            if (_currentModel == null)
            {
                Console.WriteLine("No model loaded.");
                WaitForKey();
                return;
            }

            Console.Write("Enter output file path: ");
            string outputPath = Console.ReadLine();

            ISolver solver = new KnapsackBranchAndBoundSolver();
            solver.Solve(_currentModel, outputPath);
            WaitForKey();
        }

        private void DrawHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"  {title}");
            Console.WriteLine(new string('=', 50));
            Console.ResetColor();
        }

        private void WaitForKey()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}