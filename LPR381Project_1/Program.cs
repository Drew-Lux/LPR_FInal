using System;
using LPR381_Project.UI;

namespace LPR381_Project
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set up console title and styling for a better UI experience
            Console.Title = "LPR381 Solver - Optimization Engine";

            ConsoleMenu menu = new ConsoleMenu();
            menu.Run();
        }
    }
}