using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffolding.NET.Extensions.EasyTier;

public static class FileDownloader
{
    private static readonly HttpClient Http = new HttpClient();

    /// <summary>
    /// 从指定 URL 下载文件到本地。
    /// </summary>
    /// <param name="url">文件地址。</param>
    /// <param name="destinationPath">保存路径。</param>
    /// <param name="progress">进度回调（0~100）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        // 确保目标目录存在
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // 请求文件
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var canReportProgress = total > 0 && progress != null;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
            81920, useAsync: true);
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;

            // 进度计算
            if (!canReportProgress) continue;
            var percent = (double)totalRead / total * 100;
            progress?.Report(percent);
        }
    }
}