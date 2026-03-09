// =============================================================================
// UI/ConsolePrompt.cs
// Handles all interactive user input via numbered lists.
// The user always enters a number — never free text — to select an option.
// =============================================================================

namespace BudgetTracker.UI;

public class ConsolePrompt
{
    // -------------------------------------------------------------------------
    // Numbered option picker
    // -------------------------------------------------------------------------

    /// <summary>
    /// Displays <paramref name="options"/> as a numbered list, prompts the user
    /// to enter a number, and returns the zero-based index of their choice.
    /// Loops until a valid number is entered.
    /// </summary>
    public int ChooseOption(string prompt, List<string> options)
    {
        if (options.Count == 0)
            throw new InvalidOperationException("No options provided to ChooseOption.");

        // If there is only one option, auto-select it without prompting.
        if (options.Count == 1)
        {
            Console.WriteLine($"\n  (Only one option available — auto-selecting: {options[0]})");
            return 0;
        }

        Console.WriteLine($"\n  {prompt}");
        for (int i = 0; i < options.Count; i++)
            Console.WriteLine($"    [{i + 1}] {options[i]}");

        while (true)
        {
            Console.Write($"\n  Enter number (1–{options.Count}): ");
            string? input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= options.Count)
                return choice - 1;   // return zero-based index

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Invalid input. Please enter a number between 1 and {options.Count}.");
            Console.ResetColor();
        }
    }

    // -------------------------------------------------------------------------
    // File path input
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prompts for a file path, re-asking until an existing file is given or
    /// the user types 'exit' to cancel.
    /// Returns null if the user cancels.
    /// </summary>
    public string? AskForFilePath(string prompt)
    {
        while (true)
        {
            Console.Write($"\n  {prompt} (or type 'exit' to cancel): ");
            string? input = Console.ReadLine()?.Trim().Trim('"');   // strip accidental quotes

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) return null;
            if (File.Exists(input)) return input;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  File not found: {input}");
            Console.ResetColor();
        }
    }

    // -------------------------------------------------------------------------
    // Yes / No confirmation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asks a yes/no question via a numbered list.
    /// Returns true for Yes, false for No.
    /// </summary>
    public bool Confirm(string question)
    {
        int choice = ChooseOption(question, new List<string> { "Yes", "No" });
        return choice == 0;
    }
}
