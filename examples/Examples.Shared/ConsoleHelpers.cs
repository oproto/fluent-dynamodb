namespace Examples.Shared;

/// <summary>
/// Provides consistent console UI helpers for example applications.
/// </summary>
public static class ConsoleHelpers
{
    /// <summary>
    /// Displays a numbered menu and returns the selected option (1-based).
    /// </summary>
    /// <param name="title">The menu title to display.</param>
    /// <param name="options">The menu options.</param>
    /// <returns>The selected option number (1-based), or 0 if invalid.</returns>
    public static int ShowMenu(string title, params string[] options)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        for (int i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {options[i]}");
        }

        Console.WriteLine();
        Console.Write("Select an option: ");

        var input = Console.ReadLine();
        
        if (int.TryParse(input, out int selection) && selection >= 1 && selection <= options.Length)
        {
            return selection;
        }

        ShowError($"Invalid option. Please enter a number between 1 and {options.Length}.");
        return 0;
    }

    /// <summary>
    /// Prompts for string input with optional validation.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="required">Whether the input is required (non-empty).</param>
    /// <returns>The user's input, or null if cancelled.</returns>
    public static string? GetInput(string prompt, bool required = true)
    {
        Console.Write($"{prompt}: ");
        var input = Console.ReadLine();

        if (required && string.IsNullOrWhiteSpace(input))
        {
            ShowError("This field is required. Please enter a value.");
            return null;
        }

        return input?.Trim();
    }

    /// <summary>
    /// Prompts for integer input with validation.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="min">Optional minimum value.</param>
    /// <param name="max">Optional maximum value.</param>
    /// <returns>The parsed integer, or null if invalid.</returns>
    public static int? GetIntInput(string prompt, int? min = null, int? max = null)
    {
        Console.Write($"{prompt}: ");
        var input = Console.ReadLine();

        if (!int.TryParse(input, out int value))
        {
            ShowError("Please enter a valid number.");
            return null;
        }

        if (min.HasValue && value < min.Value)
        {
            ShowError($"Value must be at least {min.Value}.");
            return null;
        }

        if (max.HasValue && value > max.Value)
        {
            ShowError($"Value must be at most {max.Value}.");
            return null;
        }

        return value;
    }

    /// <summary>
    /// Prompts for decimal input with validation.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <returns>The parsed decimal, or null if invalid.</returns>
    public static decimal? GetDecimalInput(string prompt)
    {
        Console.Write($"{prompt}: ");
        var input = Console.ReadLine();

        if (!decimal.TryParse(input, out decimal value))
        {
            ShowError("Please enter a valid decimal number.");
            return null;
        }

        return value;
    }

    /// <summary>
    /// Displays a formatted table of data with aligned columns.
    /// </summary>
    /// <typeparam name="T">The type of items to display.</typeparam>
    /// <param name="items">The items to display.</param>
    /// <param name="columns">Column definitions with header and value selector.</param>
    public static void DisplayTable<T>(IEnumerable<T> items, params (string Header, Func<T, string> Selector)[] columns)
    {
        var itemList = items.ToList();
        
        if (itemList.Count == 0)
        {
            Console.WriteLine("  (No items to display)");
            return;
        }

        // Calculate column widths
        var widths = columns.Select((col, i) => 
            Math.Max(col.Header.Length, itemList.Max(item => col.Selector(item)?.Length ?? 0))
        ).ToArray();

        // Print header
        Console.WriteLine();
        var headerLine = string.Join(" | ", columns.Select((col, i) => col.Header.PadRight(widths[i])));
        Console.WriteLine($"  {headerLine}");
        Console.WriteLine($"  {new string('-', headerLine.Length)}");

        // Print rows
        foreach (var item in itemList)
        {
            var rowLine = string.Join(" | ", columns.Select((col, i) => 
                (col.Selector(item) ?? "").PadRight(widths[i])));
            Console.WriteLine($"  {rowLine}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Displays a success message.
    /// </summary>
    /// <param name="message">The success message.</param>
    public static void ShowSuccess(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Displays an error message without exposing stack traces.
    /// </summary>
    /// <param name="message">The error message.</param>
    public static void ShowError(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ {message}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Displays an error from an exception without exposing stack traces.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="context">Optional context about what operation failed.</param>
    public static void ShowError(Exception ex, string? context = null)
    {
        var message = context != null 
            ? $"{context}: {ex.Message}" 
            : ex.Message;
        
        ShowError(message);
    }

    /// <summary>
    /// Displays an informational message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void ShowInfo(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"ℹ {message}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Displays a section header.
    /// </summary>
    /// <param name="title">The section title.</param>
    public static void ShowSection(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"── {title} ──");
    }

    /// <summary>
    /// Waits for the user to press any key to continue.
    /// </summary>
    public static void WaitForKey(string message = "Press any key to continue...")
    {
        Console.WriteLine();
        Console.WriteLine(message);
        Console.ReadKey(true);
    }
}
