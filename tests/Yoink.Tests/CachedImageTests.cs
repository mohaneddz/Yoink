using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Yoink.Presentation.Services;

namespace Yoink.Tests;

public class CachedImageTests
{
    [Fact]
    public async Task Image_binding_loads_off_thread_and_reuses_the_decoded_bitmap()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), "yoink-image-binding-" + Guid.NewGuid().ToString("N") + ".png");
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            try
            {
                var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(path)) encoder.Save(file);
                var image = new Image();
                var url = new Uri(path).AbsoluteUri;
                CachedImage.SetSource(image, url);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        if (image.Source is null)
                        {
                            if (DateTime.UtcNow > deadline) throw new TimeoutException("Cached image binding did not complete.");
                            return;
                        }
                        Assert.True(image.Source.IsFrozen);
                        File.Delete(path);
                        var reused = new Image();
                        CachedImage.SetSource(reused, url);
                        Assert.Same(image.Source, reused.Source);
                        frame.Continue = false;
                    }
                    catch (Exception ex) { completed.TrySetException(ex); frame.Continue = false; }
                };
                timer.Start();
                Dispatcher.PushFrame(frame);
                completed.TrySetResult();
            }
            catch (Exception ex) { completed.TrySetException(ex); }
            finally { timer.Stop(); File.Delete(path); dispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
