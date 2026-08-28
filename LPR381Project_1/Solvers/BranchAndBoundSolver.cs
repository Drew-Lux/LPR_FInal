using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    public class BranchAndBoundSolver : ISolver
    {
        private double _bestZ;
        private double[] _bestVariables;
        private int _nodeCounter = 0;

        public SimplexResult Solve(LinearModel rootModel, string outputFilePath)
        {
            // Initialize best bounds. Negative infinity for Max, Positive for Min.
            _bestZ = rootModel.OptimizationType == "max" ? double.MinValue : double.MaxValue;
            _bestVariables = new double[rootModel.ObjectiveCoefficients.Count];

            // Use a Stack to strictly enforce Depth-First Search (Backtracking)
            Stack<LpNode> activeNodes = new Stack<LpNode>();

            activeNodes.Push(new LpNode { NodeID = 0, Model = rootModel, ParentZ = 0 });

            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                writer.WriteLine("=========================================");
                writer.WriteLine("   BRANCH AND BOUND SIMPLEX ALGORITHM");
                writer.WriteLine("=========================================\n");

                while (activeNodes.Count > 0)
                {
                    // Backtracking: Popping from the stack returns to the most recent unexplored branch
                    LpNode currentNode = activeNodes.Pop();

                    writer.WriteLine($"\n--- Evaluating Node {currentNode.NodeID} [{currentNode.BranchHistory}] ---");

                    // 1. Solve the Sub-Problem (LP Relaxation)
                    var result = SolveSubProblem(currentNode.Model, writer);

                    // 2. FATHOMING LOGIC

                    // Fathom Condition 1: Infeasible (or unbounded)
                    if (!result.IsFeasible)
                    {
                        writer.WriteLine($"Node {currentNode.NodeID} Fathomed: {(result.IsUnbounded ? "Unbounded" : "Infeasible")} sub-problem.");
                        continue;
                    }

                    writer.WriteLine($"Relaxation Z = {result.Z:F3}");
                    for (int i = 0; i < result.Variables.Length; i++)
                    {
                        writer.WriteLine($"x{i + 1} = {result.Variables[i]:F3}");
                    }

                    // Fathom Condition 2: Bound (Worse than our best known candidate)
                    if ((rootModel.OptimizationType == "max" && result.Z <= _bestZ) ||
                        (rootModel.OptimizationType == "min" && result.Z >= _bestZ))
                    {
                        writer.WriteLine($"Node {currentNode.NodeID} Fathomed: Bound (Z = {result.Z:F3} is worse than Best Candidate {_bestZ:F3}).");
                        continue;
                    }

                    // 3. Integrality Check
                    int fractionalVarIndex = GetFirstFractionalVariable(result.Variables, rootModel.SignRestrictions);

                    if (fractionalVarIndex == -1) // Fathom Condition 3: Integer Solution Found
                    {
                        writer.WriteLine($"Node {currentNode.NodeID} Fathomed: Integer Solution Found.");
                        _bestZ = result.Z;
                        _bestVariables = result.Variables;
                        writer.WriteLine($"*** New Best Candidate Found: Z = {_bestZ:F3} ***");
                        continue;
                    }

                    // 4. BRANCHING LOGIC[cite: 1]
                    // If we reach here, the solution is better than our best bound, but contains fractions.
                    double fractionalValue = result.Variables[fractionalVarIndex];
                    int floorVal = (int)Math.Floor(fractionalValue);
                    int ceilVal = (int)Math.Ceiling(fractionalValue);

                    writer.WriteLine($"Branching on fractional variable x{fractionalVarIndex + 1} = {fractionalValue:F3}");

                    // Create Left Branch (<= floor)
                    _nodeCounter++;
                    LinearModel leftModel = CloneModelAndAddConstraint(currentNode.Model, fractionalVarIndex, "<=", floorVal);
                    activeNodes.Push(new LpNode
                    {
                        NodeID = _nodeCounter,
                        Model = leftModel,
                        BranchHistory = $"{currentNode.BranchHistory} -> x{fractionalVarIndex + 1}<={floorVal}"
                    });

                    // Create Right Branch (>= ceiling)
                    _nodeCounter++;
                    LinearModel rightModel = CloneModelAndAddConstraint(currentNode.Model, fractionalVarIndex, ">=", ceilVal);
                    activeNodes.Push(new LpNode
                    {
                        NodeID = _nodeCounter,
                        Model = rightModel,
                        BranchHistory = $"{currentNode.BranchHistory} -> x{fractionalVarIndex + 1}>={ceilVal}"
                    });
                }

                // Display Best Candidate[cite: 1]
                writer.WriteLine("\n=========================================");
                writer.WriteLine("          FINAL BEST CANDIDATE           ");
                writer.WriteLine("=========================================");

                if (_bestZ == double.MinValue || _bestZ == double.MaxValue)
                {
                    writer.WriteLine("No feasible integer solution exists.");
                }
                else
                {
                    writer.WriteLine($"Optimal Z = {_bestZ:F3}");
                    for (int i = 0; i < _bestVariables.Length; i++)
                    {
                        writer.WriteLine($"x{i + 1} = {_bestVariables[i]:F3}");
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nBranch and Bound complete. Results saved to {outputFilePath}");
            Console.ResetColor();

            return null;
        }

        // --- Helper Methods ---

        private int GetFirstFractionalVariable(double[] variables, List<string> restrictions)
        {
            double tolerance = 0.00001; // Avoid floating point precision issues
            for (int i = 0; i < variables.Length; i++)
            {
                if (restrictions[i] == "int" || restrictions[i] == "bin")
                {
                    if (Math.Abs(variables[i] - Math.Round(variables[i])) > tolerance)
                    {
                        return i; // Found a variable that should be integer but is fractional
                    }
                }
            }
            return -1; // All restricted variables are integers
        }

        private LinearModel CloneModelAndAddConstraint(LinearModel original, int varIndex, string relation, double rhs)
        {
            // Deep clone the model
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

            // Add the new branching constraint (e.g., x1 <= 2)
            Constraint branchConstraint = new Constraint
            {
                Relation = relation,
                RHS = rhs,
                Coefficients = new List<double>(new double[original.ObjectiveCoefficients.Count])
            };
            branchConstraint.Coefficients[varIndex] = 1.0;

            newModel.Constraints.Add(branchConstraint);

            return newModel;
        }

        private LpRelaxationResult SolveSubProblem(LinearModel model, StreamWriter writer)
        {
            return LpRelaxationSolver.Solve(model);
        }
    }
}