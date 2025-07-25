using System.Text;

namespace FFPBDotNet;

public class ConsoleProgressBar : IDisposable
{
    private readonly object _lock = new();
    private readonly TextWriter _output;
    private readonly int _totalTicks;
    private readonly string _description;
    private readonly bool _dynamicColumns;
    private readonly string _unit;
    private readonly bool _isWindows;

    private int _currentTick;
    private DateTime _startTime;
    private string _lastRendered = string.Empty;
    private bool _disposed;

    public ConsoleProgressBar(int totalTicks, string description, TextWriter? output = null, 
        bool dynamicColumns = true, string unit = " items", bool isWindows = false)
    {
        _totalTicks = totalTicks;
        _description = description;
        _output = output ?? Console.Error;
        _dynamicColumns = dynamicColumns;
        _unit = unit;
        _isWindows = isWindows;
        _startTime = DateTime.Now;
        _currentTick = 0;

        // Initial render
        Render();
    }

    public int CurrentTick => _currentTick;

    public void Tick(int increment = 1)
    {
        if (_disposed) return;

        lock (_lock)
        {
            _currentTick = Math.Min(_currentTick + increment, _totalTicks);
            Render();
        }
    }

    private void Render()
    {
        if (_disposed) return;

        try
        {
            var elapsed = DateTime.Now - _startTime;
            var progress = _totalTicks > 0 ? (double)_currentTick / _totalTicks : 0;
            var barWidth = GetBarWidth();
            
            // Build the progress line
            var sb = new StringBuilder();

            // Description
            if (!string.IsNullOrEmpty(_description))
            {
                sb.Append(_description);
                sb.Append(": ");
            }

            // Percentage
            sb.Append($"{progress:P0} ");

            // Progress bar
            sb.Append('|');
            var filledWidth = (int)(progress * barWidth);
            var progressChar = _isWindows ? '#' : '█';
            var emptyChar = _isWindows ? '-' : '░';
            
            sb.Append(new string(progressChar, filledWidth));
            sb.Append(new string(emptyChar, barWidth - filledWidth));
            sb.Append('|');

            // Stats
            if (_totalTicks > 0)
            {
                sb.Append($" {_currentTick}/{_totalTicks}");
            }
            else
            {
                sb.Append($" {_currentTick}");
            }

            if (!string.IsNullOrEmpty(_unit) && _unit != " items")
            {
                sb.Append(_unit);
            }

            // Time information
            if (elapsed.TotalSeconds > 0)
            {
                sb.Append($" [{elapsed:mm\\:ss}");
                
                if (progress > 0 && _totalTicks > 0)
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
            _output.Write($"\r{new string(' ', Math.Max(_lastRendered.Length, currentLine.Length))}\r{currentLine}");
            _output.Flush();
            
            _lastRendered = currentLine;
        }
        catch
        {
            // Ignore rendering errors to prevent crashes
        }
    }

    private int GetBarWidth()
    {
        if (!_dynamicColumns)
            return 20;

        try
        {
            var consoleWidth = Console.WindowWidth;
            var reservedSpace = _description?.Length ?? 0;
            reservedSpace += 50; // Space for percentage, counts, time, etc.
            
            var availableWidth = Math.Max(20, consoleWidth - reservedSpace);
            return Math.Min(60, availableWidth);
        }
        catch
        {
            return 20; // Fallback if console width detection fails
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            _disposed = true;
            
            // Final render to complete the progress bar
            if (_currentTick < _totalTicks && _totalTicks > 0)
            {
                _currentTick = _totalTicks;
            }
            Render();
            
            // Move to next line
            _output.WriteLine();
            _output.Flush();
        }
    }
}