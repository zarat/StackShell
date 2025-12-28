using System.Threading;

namespace Outlook;

internal static class StaThread
{
    public static T Run<T>(Func<T> fn)
    {
        T? result = default;
        Exception? error = null;

        using var done = new ManualResetEvent(false);

        var thread = new Thread(() =>
        {
            try { result = fn(); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        done.WaitOne();
        if (error != null) throw error;
        return result!;
    }
}
