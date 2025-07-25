// <copyright file="ConsoleProgressBar.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Text;

namespace FFPBDotNet;

/// <summary>
/// A simple console progress bar implementation.
/// </summary>
public class ConsoleProgressBar : IDisposable
{
    private readonly object @lock = new();
    private readonly TextWriter output;
    private readonly int totalTicks;
    private readonly string description;
    private readonly bool dynamicColumns;
    private readonly string unit;
    private readonly bool isWindows;

    private int currentTick;
    private DateTime startTime;
    private string lastRendered = string.Empty;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleProgressBar"/> class.
    /// </summary>
    /// <param name="totalTicks">The total number of ticks (steps) for the progress bar.</param>
    /// <param name="description">A description to display alongside the progress bar.</param>
    /// <param name="output">The <see cref="TextWriter"/> to which the progress bar will be written. Defaults to <see cref="Console.Error"/> if null.</param>
    /// <param name="dynamicColumns">Indicates whether the progress bar should dynamically adjust its width based on the console window size.</param>
    /// <param name="unit">The unit label to display after the progress count (e.g., " items").</param>
    /// <param name="isWindows">Specifies whether the progress bar should use Windows-specific rendering behavior.</param>
    public ConsoleProgressBar(int totalTicks, string description, TextWriter? output = null, bool dynamicColumns = true, string unit = " items", bool isWindows = false)
    {
        this.totalTicks = totalTicks;
        this.description = description;
        this.output = output ?? Console.Error;
        this.dynamicColumns = dynamicColumns;
        this.unit = unit;
        this.isWindows = isWindows;
        this.startTime = DateTime.Now;
        this.currentTick = 0;

        // Initial render
        this.Render();
    }

    /// <summary>
    /// Gets the total number of ticks (steps) for the progress bar.
    /// </summary>
    public int CurrentTick => this.currentTick;

    /// <summary>
    /// Increments the progress bar by one tick (step).
    /// </summary>
    /// <param name="increment">The increment.</param>
    public void Tick(int increment = 1)
    {
        if (this.disposed)
        {
            return;
        }

        lock (this.@lock)
        {
            this.currentTick = Math.Min(this.currentTick + increment, this.totalTicks);
            this.Render();
        }
    }

    /// <summary>
    /// Disposes of the progress bar, cleaning up any resources used by it.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        lock (this.@lock)
        {
            this.disposed = true;

            // Final render to complete the progress bar
            if (this.currentTick < this.totalTicks && this.totalTicks > 0)
            {
                this.currentTick = this.totalTicks;
            }

            this.Render();

            // Move to next line
            this.output.WriteLine();
            this.output.Flush();
        }
    }

    private void Render()
    {
        if (this.disposed)
        {
            return;
        }

        try
        {
            var elapsed = DateTime.Now - this.startTime;
            var progress = this.totalTicks > 0 ? (double)this.currentTick / this.totalTicks : 0;
            var barWidth = this.GetBarWidth();

            // Build the progress line
            var sb = new StringBuilder();

            // Description
            if (!string.IsNullOrEmpty(this.description))
            {
                sb.Append(this.description);
                sb.Append(": ");
            }

            // Percentage
            sb.Append($"{progress:P0} ");

            // Progress bar
            sb.Append('|');
            var filledWidth = (int)(progress * barWidth);
            var progressChar = this.isWindows ? '#' : '█';
            var emptyChar = this.isWindows ? '-' : '░';

            sb.Append(new string(progressChar, filledWidth));
            sb.Append(new string(emptyChar, barWidth - filledWidth));
            sb.Append('|');

            // Stats
            if (this.totalTicks > 0)
            {
                sb.Append($" {this.currentTick}/{this.totalTicks}");
            }
            else
            {
                sb.Append($" {this.currentTick}");
            }

            if (!string.IsNullOrEmpty(this.unit) && this.unit != " items")
            {
                sb.Append(this.unit);
            }

            // Time information
            if (elapsed.TotalSeconds > 0)
            {
                sb.Append($" [{elapsed:mm\\:ss}");

                if (progress > 0 && this.totalTicks > 0)
                {
                    var estimatedTotal = TimeSpan.FromSeconds(elapsed.TotalSeconds / progress);
                    var remaining = estimatedTotal - elapsed;
                    if (remaining.TotalSeconds > 0)
                    {
                        sb.Append($"<{remaining:mm\\:ss}");
                    }
                }

                sb.Append(']');
            }

            var currentLine = sb.ToString();

            // Clear the current line and write the new content
            this.output.Write($"\r{new string(' ', Math.Max(this.lastRendered.Length, currentLine.Length))}\r{currentLine}");
            this.output.Flush();

            this.lastRendered = currentLine;
        }
        catch
        {
            // Ignore rendering errors to prevent crashes
        }
    }

    private int GetBarWidth()
    {
        if (!this.dynamicColumns)
        {
            return 20;
        }

        try
        {
            var consoleWidth = Console.WindowWidth;
            var reservedSpace = this.description?.Length ?? 0;
            reservedSpace += 50; // Space for percentage, counts, time, etc.

            var availableWidth = Math.Max(20, consoleWidth - reservedSpace);
            return Math.Min(60, availableWidth);
        }
        catch
        {
            return 20; // Fallback if console width detection fails
        }
    }
}