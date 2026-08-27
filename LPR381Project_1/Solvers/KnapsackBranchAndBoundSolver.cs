using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LPR381_Project.Models;

namespace LPR381_Project.Solvers
{
    // Represents a node specifically for the Knapsack tree
    public class KnapsackNode
    {
        public int Level { get; set; } // Current item index being considered
        public double CurrentValue { get; set; } // Accumulated Z value
        public double CurrentWeight { get; set; } // Accumulated weight
        public double Bound { get; set; } // Upper bound for fathoming
        public List<int> SelectedItems { get; set; } = new List<int>(); // 1 if included, 0 if excluded
    }

    public class KnapsackBranchAndBoundSolver : ISolver
    {
        public SimplexResult Solve(LinearModel model, string outputFilePath)
        {
            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                writer.WriteLine("=========================================");
                writer.WriteLine("   BRANCH & BOUND KNAPSACK ALGORITHM     ");
                writer.WriteLine("=========================================\n");

                // Knapsack typically utilizes one primary weight constraint
                if (model.Constraints.Count == 0)
                {
                    writer.WriteLine("Error: Knapsack requires at least one capacity constraint.");
                    return null;
                }

                int numItems = model.ObjectiveCoefficients.Count;
                double capacity = model.Constraints[0].RHS;
                List<double> weights = model.Constraints[0].Coefficients;
                List<double> values = model.ObjectiveCoefficients;

                // Backtracking structure
                Stack<KnapsackNode> stack = new Stack<KnapsackNode>();

                // Root Node
                KnapsackNode root = new KnapsackNode { Level = -1, CurrentValue = 0, CurrentWeight = 0 };
                root.Bound = CalculateBound(root, capacity, numItems, weights, values);
                stack.Push(root);

                double bestValue = 0;
                List<int> bestCombination = new List<int>(new int[numItems]);
                int nodeCount = 0;

                while (stack.Count > 0)
                {
                    KnapsackNode current = stack.Pop();
                    nodeCount++;

                    writer.WriteLine($"\n--- Evaluating Node {nodeCount} (Level: {current.Level}) ---");
                    writer.WriteLine($"Current Value: {current.CurrentValue}, Current Weight: {current.CurrentWeight}");
                    writer.WriteLine($"Calculated Bound: {current.Bound:F3}");

                    // Fathom Condition 1: Bound is worse than or equal to our best known candidate
                    if (current.Bound <= bestValue)
                    {
                        writer.WriteLine($"Node {nodeCount} Fathomed: Bound ({current.Bound:F3}) is not better than Best Candidate ({bestValue}).");
                        continue;
                    }

                    // Fathom Condition 2: Reached the end of the decision tree (all items considered)[cite: 1]
                    if (current.Level == numItems - 1)
                    {
                        continue;
                    }

                    int nextLevel = current.Level + 1;

                    // Branch 1: Include the next item (x = 1)[cite: 1]
                    double nextWeight = current.CurrentWeight + weights[nextLevel];
                    if (nextWeight <= capacity)
                    {
                        KnapsackNode includeNode = new KnapsackNode
                        {
                            Level = nextLevel,
                            CurrentWeight = nextWeight,
                            CurrentValue = current.CurrentValue + values[nextLevel],
                            SelectedItems = new List<int>(current.SelectedItems)
                        };
                        includeNode.SelectedItems.Add(1);
                        includeNode.Bound = CalculateBound(includeNode, capacity, numItems, weights, values);

                        if (includeNode.CurrentValue > bestValue)
                        {
                            bestValue = includeNode.CurrentValue;
                            // Pad remaining unvisited items with 0 for the best combination record
                            bestCombination = new List<int>(includeNode.SelectedItems);
                            while (bestCombination.Count < numItems) bestCombination.Add(0);

                            writer.WriteLine($"*** New Best Candidate Found: Z = {bestValue} ***");
                        }

                        if (includeNode.Bound > bestValue)
                        {
                            stack.Push(includeNode);
                        }
                    }
                    else
                    {
                        writer.WriteLine($"Branch (Include x{nextLevel + 1}) Fathomed: Exceeds Capacity.");
                    }

                    // Branch 2: Exclude the next item (x = 0)[cite: 1]
                    KnapsackNode excludeNode = new KnapsackNode
                    {
                        Level = nextLevel,
                        CurrentWeight = current.CurrentWeight,
                        CurrentValue = current.CurrentValue,
                        SelectedItems = new List<int>(current.SelectedItems)
                    };
                    excludeNode.SelectedItems.Add(0);
                    excludeNode.Bound = CalculateBound(excludeNode, capacity, numItems, weights, values);

                    if (excludeNode.Bound > bestValue)
                    {
                        stack.Push(excludeNode);
                    }
                }

                // Display Best Candidate[cite: 1]
                writer.WriteLine("\n=========================================");
                writer.WriteLine("          FINAL BEST CANDIDATE           ");
                writer.WriteLine("=========================================");
                writer.WriteLine($"Optimal Z = {bestValue}");
                for (int i = 0; i < numItems; i++)
                {
                    writer.WriteLine($"x{i + 1} = {bestCombination[i]}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nKnapsack Branch & Bound complete. Results saved to {outputFilePath}");
            Console.ResetColor();

            return null;
        }

        // Calculates the fractional LP relaxation bound for a node
        private double CalculateBound(KnapsackNode node, double capacity, int numItems, List<double> weights, List<double> values)
        {
            if (node.CurrentWeight >= capacity) return 0;

            double boundValue = node.CurrentValue;
            double totalWeight = node.CurrentWeight;
            int j = node.Level + 1;

            // Greedily add items whole
            while (j < numItems && totalWeight + weights[j] <= capacity)
            {
                totalWeight += weights[j];
                boundValue += values[j];
                j++;
            }

            // Add the fractional part of the next item
            if (j < numItems)
            {
                boundValue += (capacity - totalWeight) * (values[j] / weights[j]);
            }

            return boundValue;
        }
    }
}