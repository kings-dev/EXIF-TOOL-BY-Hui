using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Hui_WPF.utils
{
    public static class ExtensionMethods
    {
        public static TResult? Let<T, TResult>(this T? self, Func<T, TResult> func) where T : class?
        {
            return self == null ? default : func(self);
        }

        public static TResult? LetValue<T, TResult>(this T? self, Func<T, TResult> func) where T : struct
        {
            return self == null ? default : func(self.Value);
        }

        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object?>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);

            CancellationTokenRegistration registration = default;
            if (cancellationToken != default && cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5) { Debug.WriteLine($"Access denied attempting to kill process {process.Id} on cancel."); }
                    catch (Win32Exception ex) { Debug.WriteLine($"Win32Exception {ex.NativeErrorCode} attempting to kill process {process.Id} on cancel: {ex.Message}"); }
                    catch (Exception ex) { Debug.WriteLine($"Unexpected error attempting to kill process {process.Id} on cancel: {ex.Message}"); }
                    finally
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                });
            }

            return tcs.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        }
    }
}