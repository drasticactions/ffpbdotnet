using System.Text;
using System.Text.RegularExpressions;

namespace FFPBDotNet;

public class ProgressNotifier : IDisposable
{
    private static readonly Regex DurationRegex = new(@"Duration: (\d{2}):(\d{2}):(\d{2})\.\d{2}", RegexOptions.Compiled);
    private static readonly Regex ProgressRegex = new(@"time=(\d{2}):(\d{2}):(\d{2})\.\d{2}", RegexOptions.Compiled);
    private static readonly Regex SourceRegex = new(@"from '(.*)':", RegexOptions.Compiled);
    private static readonly Regex FpsRegex = new(@"(\d{2}\.\d{2}|\d{2}) fps", RegexOptions.Compiled);

    private readonly List<string> _lines = new();
    private readonly StringBuilder _lineAccumulator = new();
    private readonly TextWriter _output;
    private readonly string _encoding;

    private int? _duration;
    private string? _source;
    private ConsoleProgressBar? _progressBar;
    private int? _fps;

    public ProgressNotifier(TextWriter? output = null, string? encoding = null)
    {
        _output = output ?? Console.Error;
        _encoding = encoding ?? Encoding.UTF8.WebName;
    }

    public void ProcessChar(char character)
    {
        if (character == '\r' || character == '\n')
        {
            var line = ProcessNewline();
            
            _duration ??= GetDuration(line);
            _source ??= GetSource(line);
            _fps ??= GetFps(line);
            
            UpdateProgress(line);
        }
        else
        {
            _lineAccumulator.Append(character);
            
            // Handle interactive prompts like "[y/N] "
            if (_lineAccumulator.Length >= 6 && _lineAccumulator.ToString().EndsWith("[y/N] "))
            {
                // Clear any existing progress bar by moving to new line
                if (_progressBar != null)
                {
                    _output.WriteLine();
                }
                
                _output.Write(_lineAccumulator.ToString());
                _output.Flush();
                ProcessNewline();
            }
        }
    }

    private string ProcessNewline()
    {
        var line = _lineAccumulator.ToString();
        _lines.Add(line);
        _lineAccumulator.Clear();
        return line;
    }

    private static int? GetFps(string line)
    {
        var match = FpsRegex.Match(line);
        if (match.Success && float.TryParse(match.Groups[1].Value, out var fps))
        {
            return (int)Math.Round(fps);
        }
        return null;
    }

    private static int? GetDuration(string line)
    {
        var match = DurationRegex.Match(line);
        if (match.Success)
        {
            var hours = int.Parse(match.Groups[1].Value);
            var minutes = int.Parse(match.Groups[2].Value);
            var seconds = int.Parse(match.Groups[3].Value);
            return (hours * 60 + minutes) * 60 + seconds;
        }
        return null;
    }

    private static string? GetSource(string line)
    {
        var match = SourceRegex.Match(line);
        if (match.Success)
        {
            return Path.GetFileName(match.Groups[1].Value);
        }
        return null;
    }

    private void UpdateProgress(string line)
    {
        var match = ProgressRegex.Match(line);
        if (!match.Success) return;

        var hours = int.Parse(match.Groups[1].Value);
        var minutes = int.Parse(match.Groups[2].Value);
        var seconds = int.Parse(match.Groups[3].Value);
        var currentSeconds = (hours * 60 + minutes) * 60 + seconds;

        var total = _duration;
        var current = currentSeconds;

        if (_fps.HasValue)
        {
            current *= _fps.Value;
            if (total.HasValue)
            {
                total *= _fps.Value;
            }
        }

        if (_progressBar == null)
        {
            var unit = _fps.HasValue ? " frames" : " seconds";
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            
            _progressBar = new ConsoleProgressBar(
                total ?? int.MaxValue, 
                _source ?? "Processing", 
                _output,
                dynamicColumns: true,
                unit: unit,
                isWindows: isWindows
            );
        }

        var ticksToUpdate = current - _progressBar.CurrentTick;
        if (ticksToUpdate > 0)
        {
            _progressBar.Tick(ticksToUpdate);
        }
    }

    public string GetLastLine()
    {
        return _lines.LastOrDefault() ?? string.Empty;
    }

    public void Dispose()
    {
        _progressBar?.Dispose();
    }
}