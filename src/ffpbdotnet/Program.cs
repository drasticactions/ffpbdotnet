// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Diagnostics;
using FFPBDotNet;

namespace FFPBDotNet;

/// <summary>
/// Main entry point for the FFPBDotNet application.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        try
        {
            using var notifier = new ProgressNotifier();

            var ffmpegArgs = new List<string> { "ffmpeg" };
            ffmpegArgs.AddRange(args);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in args)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.Error.WriteLine("Exiting.");
                Environment.Exit(128 + 2); // SIGINT + 128
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start ffmpeg process");
                return 1;
            }

            var buffer = new char[1];
            var reader = process.StandardError;

            var stdinTask = Task.Run(async () =>
            {
                try
                {
                    while (!process.HasExited)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(false);
                            if (key.Key == ConsoleKey.Enter)
                            {
                                await process.StandardInput.WriteLineAsync();
                            }
                            else
                            {
                                await process.StandardInput.WriteAsync(key.KeyChar);
                            }

                            await process.StandardInput.FlushAsync();
                        }

                        await Task.Delay(50);
                    }
                }
                catch
                {
                    // Ignore stdin handling errors
                }
            });

            while (!process.HasExited)
            {
                var bytesRead = await reader.ReadAsync(buffer, 0, 1);
                if (bytesRead > 0)
                {
                    notifier.ProcessChar(buffer[0]);
                }
                else
                {
                    await Task.Delay(10);
                }
            }

            try
            {
                await stdinTask.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Ignore timeout or cancellation
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(notifier.GetLastLine());
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected exception: {ex.Message}");
            return 1;
        }
    }

    private static void ShowHelp()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        Console.WriteLine($"ffpb v{version}");
        Console.WriteLine("A progress bar wrapper for ffmpeg");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ffpb [ffmpeg options]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ffpb -i input.mp4 -c:v libx264 -crf 23 output.mp4");
        Console.WriteLine("  ffpb -i input.avi -c:v copy -c:a aac output.mp4");
        Console.WriteLine("  ffpb -i input.mov -vf scale=1280:720 -c:v libx264 output.mp4");
        Console.WriteLine();
        Console.WriteLine("This tool wraps ffmpeg and displays a progress bar during conversion.");
        Console.WriteLine("All ffmpeg options are supported - just pass them as arguments.");
    }
}
