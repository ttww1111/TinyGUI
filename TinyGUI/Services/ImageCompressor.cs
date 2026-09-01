using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TinifyAPI;
using TinyGUI.Models;

namespace TinyGUI.Services
{
    /// <summary>
    /// 压缩模式
    /// </summary>
    public enum CompressMode
    {
        Compress, // 仅压缩
        Scale,
        Fit,
        Cover,
        Thumb
    }

    /// <summary>
    /// 单次压缩的选项
    /// </summary>
    public class CompressOptions
    {
        public CompressMode Mode { get; set; } = CompressMode.Compress;
        public uint Width { get; set; }
        public uint Height { get; set; }
        public bool MetaCopyright { get; set; }
        public bool MetaLocation { get; set; }
        public bool MetaCreationTime { get; set; }
    }

    /// <summary>
    /// 单张图片的压缩结果。
    /// 注意：结果总是先写到新文件（OutputPath），是否替换原图由调用方在批次结束后决定
    /// —— 这样用户能看到实际节省了多少再决定，而不是压缩前盲选。
    /// </summary>
    public class CompressResult
    {
        public bool Success { get; set; }
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
        public double SavedPercent { get; set; }
        public string ErrorMessage { get; set; }
        /// <summary>本次请求后的月度已用压缩次数（来自 Tinify.CompressionCount）</summary>
        public uint? CompressionCount { get; set; }
        /// <summary>本次实际使用的 API Key（原值，用于按 Key 统计额度，展示时才脱敏）</summary>
        public string Key { get; set; }
        /// <summary>压缩结果写入的路径（新文件，与原文件同目录）</summary>
        public string OutputPath { get; set; }
    }

    /// <summary>
    /// 封装 Tinify 客户端：逐张容错、429 轮换 Key、返回体积对比。
    /// </summary>
    public class ImageCompressor
    {
        private readonly KeyPool _keyPool;

        public ImageCompressor(KeyPool keyPool)
        {
            _keyPool = keyPool;
        }

        /// <summary>
        /// 429（额度用尽）时在同一 Key 上的最多尝试次数。
        /// TinyPNG 的 429 既可能是额度真用完，也可能是请求太快被限流；
        /// 先退避重试几次，避免把"限流"误判成"额度耗尽"而白白浪费还有额度的 Key。
        /// </summary>
        private const int MaxRateLimitRetries = 3;

        /// <summary>网络等异常的单张重试次数（失败不计费，重试是安全的）</summary>
        private const int MaxAttempts = 3;

        public async Task<CompressResult> CompressAsync(string inputPath, CompressOptions options, CancellationToken ct, Action<string> log = null)
        {
            var result = new CompressResult { OriginalSize = GetFileSize(inputPath) };
            string name = Path.GetFileName(inputPath);
            string outputPath = BuildOutputPath(inputPath);
            result.OutputPath = outputPath;

            int rateLimitRetries = 0;
            int attempts = 0;

            while (_keyPool.AnyAvailable)
            {
                ct.ThrowIfCancellationRequested();

                string key = _keyPool.ActiveKey;
                if (key == null) break;

                // 通过闸门设置 Key：同 Key 并行，换 Key 前先等在飞请求落地
                TinifyKeyGate.Enter(key);
                try
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        log?.Invoke($"↑ 上传 {name}（{ImageItem.FormatSize(result.OriginalSize)}）");

                        Source source = await Tinify.FromFile(inputPath);
                        log?.Invoke($"  已上传，服务端压缩中…（{sw.Elapsed.TotalSeconds:F1}s）");

                        source = ApplyPreserve(source, options);
                        if (options.Mode != CompressMode.Compress)
                            source = source.Resize(BuildResize(options));

                        await source.ToFile(outputPath);
                        sw.Stop();

                        result.Success = true;
                        result.CompressedSize = GetFileSize(outputPath);
                        result.SavedPercent = result.OriginalSize > 0
                            ? (1.0 - (double)result.CompressedSize / result.OriginalSize) * 100.0
                            : 0;
                        result.CompressionCount = Tinify.CompressionCount;
                        result.Key = key;

                        log?.Invoke($"↓ 完成 {name}：{ImageItem.FormatSize(result.OriginalSize)} → {ImageItem.FormatSize(result.CompressedSize)}" +
                                    $"（省 {result.SavedPercent:F1}%，{sw.Elapsed.TotalSeconds:F1}s，Key ****{KeyTail(key)}）");
                        return result;
                    }
                    catch (TinifyException ex) when (ex.Status == 429)
                    {
                        rateLimitRetries++;
                        if (rateLimitRetries < MaxRateLimitRetries)
                        {
                            int delay = 1000 * rateLimitRetries; // 1s、2s 退避
                            log?.Invoke($"! Key ****{KeyTail(key)} 返回 429，{delay / 1000}s 后重试（{rateLimitRetries}/{MaxRateLimitRetries - 1}）");
                            await Task.Delay(delay, ct);
                            continue;
                        }

                        // 退避重试后仍是 429，才判定为该 Key 额度耗尽
                        _keyPool.MarkExhausted(key);
                        _keyPool.Rotate();
                        rateLimitRetries = 0;
                        log?.Invoke($"! Key ****{KeyTail(key)} 额度已用尽，切换到下一个 Key");
                        result.ErrorMessage = "API Key 额度已用尽，尝试下一个 Key";
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        if (attempts < MaxAttempts)
                        {
                            log?.Invoke($"! {name} 异常（{ex.Message}），1s 后重试（{attempts}/{MaxAttempts - 1}）");
                            await Task.Delay(1000, ct);
                            continue;
                        }
                        log?.Invoke($"✗ {name} 失败：{ex.Message}（Key ****{KeyTail(key)}）");
                        result.ErrorMessage = ex.Message;
                        return result;
                    }
                }
                finally
                {
                    TinifyKeyGate.Exit();
                }
            }

            if (string.IsNullOrEmpty(result.ErrorMessage))
            {
                result.ErrorMessage = _keyPool.Count == 0
                    ? "未配置任何 API Key"
                    : "所有 API Key 本月额度已用完";
            }
            log?.Invoke($"✗ {name}：{result.ErrorMessage}");
            return result;
        }

        /// <summary>Key 只显示后 4 位，避免日志里泄露完整密钥</summary>
        private static string KeyTail(string key)
            => string.IsNullOrEmpty(key) ? "?" : (key.Length > 4 ? key.Substring(key.Length - 4) : key);

        private static Source ApplyPreserve(Source source, CompressOptions options)
        {
            var metas = new System.Collections.Generic.List<string>();
            if (options.MetaCopyright) metas.Add("copyright");
            if (options.MetaLocation) metas.Add("location");
            if (options.MetaCreationTime) metas.Add("creation");
            return metas.Count > 0 ? source.Preserve(metas.ToArray()) : source;
        }

        private static object BuildResize(CompressOptions options)
        {
            switch (options.Mode)
            {
                case CompressMode.Scale:
                    // scale 只需 width 或 height 其一
                    if (options.Width > 0)
                        return new { method = "scale", width = options.Width };
                    if (options.Height > 0)
                        return new { method = "scale", height = options.Height };
                    return new { method = "scale", width = options.Width };
                default:
                    return new { method = options.Mode.ToString().ToLowerInvariant(), width = options.Width, height = options.Height };
            }
        }

        /// <summary>
        /// 不替换原文件时，结果统一命名为「原名_tiny.ext」。
        /// 同名文件已存在就加序号（_tiny_1、_tiny_2…），避免覆盖上一次的压缩结果。
        /// </summary>
        private static string BuildOutputPath(string inputPath)
        {
            string extension = Path.GetExtension(inputPath).ToLowerInvariant();
            string fileName = Path.GetFileName(inputPath);
            string directory = Path.GetDirectoryName(inputPath);
            string baseName = fileName.Substring(0, fileName.Length - extension.Length);

            string candidate = Path.Combine(directory, $"{baseName}_tiny{extension}");
            int i = 1;
            while (File.Exists(candidate))
                candidate = Path.Combine(directory, $"{baseName}_tiny_{i++}{extension}");
            return candidate;
        }

        private static long GetFileSize(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }
    }
}
