using System.Diagnostics;
using FFPBDotNet;

namespace FFPBDotNet;

class Program
{
    static async Task<int> Main(string[] args)
    {
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
                CreateNoWindow = true
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

            // Start a task to handle stdin forwarding
            var stdinTask = Task.Run(async () =>
            {
                try
                {
                    while (!process.HasExited)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(false); // Echo to console (false = show the character)
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
                    // Small delay to prevent tight loop
                    await Task.Delay(10);
                }
            }

            // Wait for stdin task to complete
            try
            {
                await stdinTask.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Ignore timeout
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
}
