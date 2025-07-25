// <copyright file="ProgressNotifier.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;

namespace FFPBDotNet;

/// <summary>
/// A class to handle progress notifications from ffmpeg output.
/// </summary>
public class ProgressNotifier(TextWriter? output = null, string? encoding = null) : IDisposable
{
    private static readonly Regex DurationRegex = new(@"Duration: (\d{2}):(\d{2}):(\d{2})\.\d{2}", RegexOptions.Compiled);
    private static readonly Regex ProgressRegex = new(@"time=(\d{2}):(\d{2}):(\d{2})\.\d{2}", RegexOptions.Compiled);
    private static readonly Regex SourceRegex = new(@"from '(.*)':", RegexOptions.Compiled);
    private static readonly Regex FpsRegex = new(@"(\d{2}\.\d{2}|\d{2}) fps", RegexOptions.Compiled);

    private readonly List<string> lines = new();
    private readonly StringBuilder lineAccumulator = new();
    private readonly TextWriter output = output ?? Console.Error;
    private readonly string encoding = encoding ?? Encoding.UTF8.WebName;

    private int? duration;
    private string? source;
    private ConsoleProgressBar? progressBar;
    private int? fps;

    /// <summary>
    /// Processes a character from the ffmpeg output.
    /// </summary>
    /// <param name="character">The character.</param>
    public void ProcessChar(char character)
    {
        if (character == '\r' || character == '\n')
        {
            var line = this.ProcessNewline();

            this.duration ??= GetDuration(line);
            this.source ??= GetSource(line);
            this.fps ??= GetFps(line);

            this.UpdateProgress(line);
        }
        else
        {
            this.lineAccumulator.Append(character);

            // Handle interactive prompts like "[y/N] "
            if (this.lineAccumulator.Length >= 6 && this.lineAccumulator.ToString().EndsWith("[y/N] "))
            {
                // Clear any existing progress bar by moving to new line
                if (this.progressBar != null)
                {
                    this.output.WriteLine();
                }

                this.output.Write(this.lineAccumulator.ToString());
                this.output.Flush();
                this.ProcessNewline();
            }
        }
    }

    /// <summary>
    /// Gets the last processed line from the ffmpeg output.
    /// </summary>
    /// <returns>The last line.</returns>
    public string GetLastLine()
    {
        return this.lines.LastOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Disposes of the progress notifier, cleaning up any resources used by the progress bar.
    /// </summary>
    public void Dispose()
    {
        this.progressBar?.Dispose();
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
            return (((hours * 60) + minutes) * 60) + seconds;
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

    private string ProcessNewline()
    {
        var line = this.lineAccumulator.ToString();
        this.lines.Add(line);
        this.lineAccumulator.Clear();
        return line;
    }

    private void UpdateProgress(string line)
    {
        var match = ProgressRegex.Match(line);
        if (!match.Success)
        {
            return;
        }

        var hours = int.Parse(match.Groups[1].Value);
        var minutes = int.Parse(match.Groups[2].Value);
        var seconds = int.Parse(match.Groups[3].Value);
        var currentSeconds = (((hours * 60) + minutes) * 60) + seconds;

        var total = this.duration;
        var current = currentSeconds;

        if (this.fps.HasValue)
        {
            current *= this.fps.Value;
            if (total.HasValue)
            {
                total *= this.fps.Value;
            }
        }

        if (this.progressBar == null)
        {
            var unit = this.fps.HasValue ? " frames" : " seconds";
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

            this.progressBar = new ConsoleProgressBar(
                total ?? int.MaxValue,
                this.source ?? "Processing",
                this.output,
                dynamicColumns: true,
                unit: unit,
                isWindows: isWindows);
        }

        var ticksToUpdate = current - this.progressBar.CurrentTick;
        if (ticksToUpdate > 0)
        {
            this.progressBar.Tick(ticksToUpdate);
        }
    }
}