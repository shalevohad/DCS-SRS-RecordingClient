using System;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.Core.Debug;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.AudioTest
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            // Configure basic console logging
            var config = new NLog.Config.LoggingConfiguration();
            var consoleTarget = new NLog.Targets.ConsoleTarget("console")
            {
                Layout = "${time} [${level:uppercase=true}] ${logger}: ${message} ${exception:format=tostring}"
            };
            config.AddTarget(consoleTarget);
            config.AddRuleForAllLevels(consoleTarget);
            LogManager.Configuration = config;

            Console.WriteLine("=== DCS SRS Audio Test Utility ===");
            Console.WriteLine();

            try
            {
                // Step 1: Diagnose audio system
                Console.WriteLine("Step 1: Diagnosing audio system...");
                var systemInfo = await AudioDiagnostics.DiagnoseAudioSystemAsync();
                Console.WriteLine(systemInfo.ToString());

                if (!string.IsNullOrEmpty(systemInfo.ErrorMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"? Audio system error detected: {systemInfo.ErrorMessage}");
                    Console.ResetColor();
                    Console.WriteLine("Please fix audio system issues before testing playback.");
                    return;
                }

                if (systemInfo.DefaultDevice?.Volume < 0.1f)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"??  WARNING: System volume is very low ({systemInfo.DefaultDevice.Volume:P0})");
                    Console.WriteLine("Consider increasing your system volume.");
                    Console.ResetColor();
                }

                if (!systemInfo.DefaultDevice?.IsEnabled == true)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("? Default audio device is disabled!");
                    Console.ResetColor();
                    return;
                }

                // Step 2: Test audio output
                Console.WriteLine();
                Console.WriteLine("Step 2: Testing audio output...");
                Console.WriteLine("You should hear a 440Hz tone for 3 seconds.");
                Console.WriteLine("Press any key to start the test, or 'q' to quit...");
                
                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    return;

                Console.WriteLine("?? Playing test tone...");
                Console.WriteLine("LISTEN FOR THE TONE NOW!");

                try
                {
                    await AudioDiagnostics.PlayTestToneAsync(440.0, 3.0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("? Test tone completed successfully!");
                    Console.ResetColor();
                    Console.WriteLine();
                    
                    Console.WriteLine("Did you hear the tone? (y/n):");
                    var heard = Console.ReadKey(true);
                    
                    if (heard.KeyChar == 'y' || heard.KeyChar == 'Y')
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("? GREAT! Your audio output is working correctly.");
                        Console.WriteLine("The issue is likely with the SRS recording playback, not your audio system.");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("? PROBLEM: You didn't hear the tone.");
                        Console.WriteLine("This indicates an issue with your audio output system:");
                        Console.WriteLine("- Check your speakers/headphones are connected");
                        Console.WriteLine("- Check Windows sound settings");
                        Console.WriteLine("- Try different audio devices");
                        Console.WriteLine("- Restart Windows Audio service");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"? Test tone FAILED: {ex.Message}");
                    Console.ResetColor();
                    Logger.Error(ex, "Test tone failed");
                    
                    Console.WriteLine();
                    Console.WriteLine("Possible solutions:");
                    Console.WriteLine("- Restart Windows Audio service (net stop AudioSrv && net start AudioSrv)");
                    Console.WriteLine("- Check for conflicting audio applications");
                    Console.WriteLine("- Try running as administrator");
                    Console.WriteLine("- Update audio drivers");
                }

                // Step 3: Test alternative audio methods
                Console.WriteLine();
                Console.WriteLine("Step 3: Testing alternative audio methods...");
                Console.WriteLine("Press any key to test different audio output methods, or 'q' to quit...");
                
                key = Console.ReadKey(true);
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    return;

                Console.WriteLine("Testing different audio output methods (you may hear multiple tones):");
                
                var methods = new[]
                {
                    ("WASAPI Shared", 440.0),
                    ("WASAPI Exclusive", 523.25), // C note
                    ("DirectSound", 659.25),      // E note  
                    ("WaveOut", 783.99)           // G note
                };

                foreach (var (name, freq) in methods)
                {
                    try
                    {
                        Console.WriteLine($"?? Testing {name} ({freq:F0}Hz)...");
                        
                        // Use reflection to call the specific method
                        var method = typeof(AudioDiagnostics).GetMethod($"PlayTestTone{name.Replace(" ", "")}Async", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        
                        if (method != null)
                        {
                            var task = (Task?)method.Invoke(null, new object[] { freq, 2.0 });
                            if (task != null)
                            {
                                await task;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"? {name} method worked!");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            // Fallback to general method
                            await AudioDiagnostics.PlayTestToneAsync(freq, 2.0);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"? {name} method worked!");
                            Console.ResetColor();
                        }
                        
                        await Task.Delay(500); // Small delay between tests
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"? {name} failed: {ex.Message}");
                        Console.ResetColor();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.ResetColor();
                Logger.Fatal(ex, "Fatal error in audio test");
            }

            Console.WriteLine();
            Console.WriteLine("=== Test Complete ===");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}