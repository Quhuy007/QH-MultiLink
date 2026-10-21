using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace QH.MultiLinkShortener.Core;

public sealed class ShorteningCoordinator
{
    public async IAsyncEnumerable<ShorteningResult> RunAsync(ShorteningRequest request,
        IReadOnlyList<IShortenerProvider> providers, int concurrency = 4, int timeoutSeconds = 20,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!UrlValidation.TryParse(request.Url.AbsoluteUri, out _))
            throw new ArgumentException("URL phải dùng http/https và không chứa thông tin đăng nhập.");
        if (providers.Select(x => x.Info.Id).Distinct().Count() != providers.Count)
            throw new ArgumentException("Không được chọn trùng dịch vụ.");
        if (request.Password is not null && providers.Any(p => !p.Info.SupportsPassword))
            throw new ArgumentException("Dịch vụ đã chọn không hỗ trợ mật khẩu.");
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutSeconds, 1);

        var channel = Channel.CreateUnbounded<ShorteningResult>(new() { SingleReader = true });
        using var slots = new SemaphoreSlim(concurrency);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var work = providers.Select(ExecuteAsync).ToArray();
        var completion = CompleteAsync();
        try
        {
            // Drain final cancellation states even after the caller cancels.
            await foreach (var result in channel.Reader.ReadAllAsync(CancellationToken.None)) yield return result;
        }
        finally
        {
            await lifetime.CancelAsync();
            await completion;
        }

        async Task CompleteAsync()
        {
            try { await Task.WhenAll(work); }
            finally { channel.Writer.TryComplete(); }
        }

        async Task ExecuteAsync(IShortenerProvider provider)
        {
            var result = new ShorteningResult(Guid.NewGuid().ToString("N"), provider.Info.Id,
                provider.Info.Name, ShorteningStatus.Pending, DateTimeOffset.UtcNow);
            channel.Writer.TryWrite(result);
            var acquired = false;
            var watch = new Stopwatch();
            try
            {
                await slots.WaitAsync(lifetime.Token);
                acquired = true;
                watch.Start();
                channel.Writer.TryWrite(result with { Status = ShorteningStatus.Running });
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                var link = await provider.ShortenAsync(request, timeout.Token);
                if (!UrlValidation.TryParse(link.Url.AbsoluteUri, out _))
                    throw new ProviderException("Dịch vụ trả về link không hợp lệ.");
                result = result with { Status = ShorteningStatus.Success, ShortUrl = link.Url.AbsoluteUri, Protection = link.Protection };
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            { result = result with { Status = ShorteningStatus.Cancelled, Message = "Đã hủy. Dịch vụ có thể đã nhận yêu cầu." }; }
            catch (OperationCanceledException)
            { result = result with { Status = ShorteningStatus.Failed, Message = "Hết thời gian chờ. Kiểm tra dịch vụ trước khi tạo lại." }; }
            catch (ProviderException ex)
            { result = result with { Status = ShorteningStatus.Failed, Message = ex.Message }; }
            catch (Exception)
            { result = result with { Status = ShorteningStatus.Failed, Message = "Không thể kết nối dịch vụ. Kiểm tra mạng hoặc thử lại sau." }; }
            finally { if (acquired) slots.Release(); }
            channel.Writer.TryWrite(result with { ElapsedMs = watch.ElapsedMilliseconds });
        }
    }
}
