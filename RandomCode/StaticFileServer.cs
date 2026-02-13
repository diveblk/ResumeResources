using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LooseChange.Scripts.Systems.Helpers
{
    public sealed class StaticFileServer : IDisposable
    {
        private HttpListener? _listener;
        private readonly string _rootFull;
        private readonly string _prefix;
        private CancellationTokenSource? _cts;
        private Thread? _thread;

        // Web UI -> Godot: queue messages so Godot can pull on main thread
        private readonly ConcurrentQueue<string> _commandQueue = new();

        // Simple in-memory cache for static files (path -> bytes + mime)
        private readonly ConcurrentDictionary<string, CachedFile> _fileCache = new();

        public int Port { get; }

        private sealed record CachedFile(byte[] Bytes, string Mime, DateTime LastWriteUtc);

        public StaticFileServer(string root, int port)
        {
            _rootFull = Path.GetFullPath(root);
            Port = port;
            _prefix = $"http://127.0.0.1:{Port}/";
        }

        public void Start()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();

            // Accept all localhost variants (Chrome often uses IPv6 ::1 for localhost)
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Prefixes.Add($"http://[::1]:{Port}/");

            _listener.Start();

            _thread = new Thread(() => Loop(_listener, _cts.Token))
            {
                IsBackground = true,
                Name = $"StaticFileServer:{Port}"
            };
            _thread.Start();
        }

        /// <summary>
        /// Called from Godot main thread to pull commands safely.
        /// </summary>
        public bool TryDequeueCommand(out string json)
            => _commandQueue.TryDequeue(out json);

        private void Loop(HttpListener listener, CancellationToken ct)
        {
            // Run an async loop on this dedicated thread
            try
            {
                Task.Run(async () =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        HttpListenerContext ctx;
                        try
                        {
                            ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        }
                        catch (ObjectDisposedException)
                        {
                            break; // listener closed
                        }
                        catch (HttpListenerException)
                        {
                            break; // listener stopped
                        }
                        catch (OperationCanceledException)
                        {
                            break; // cancellation
                        }
                        catch
                        {
                            break;
                        }

                        _ = Task.Run(() => HandleAsync(ctx), ct);
                    }
                }, CancellationToken.None)  // IMPORTANT: don't bind ct to Task.Run here
                .GetAwaiter()
                .GetResult();               // IMPORTANT: no Wait(ct)
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown; ignore
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            try
            {
                // ---------- /godot endpoint ----------
                if (ctx.Request.HttpMethod == "OPTIONS" && ctx.Request.Url?.AbsolutePath == "/godot")
                {
                    AddCorsHeaders(ctx.Response);
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url?.AbsolutePath == "/godot")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    string json = await reader.ReadToEndAsync().ConfigureAwait(false);

                    // IMPORTANT: DO NOT invoke Godot events from this thread.
                    _commandQueue.Enqueue(json);

                    AddCorsHeaders(ctx.Response);
                    ctx.Response.Headers["Cache-Control"] = "no-store";
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json; charset=utf-8";

                    var respBytes = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
                    ctx.Response.ContentLength64 = respBytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(respBytes, 0, respBytes.Length).ConfigureAwait(false);
                    ctx.Response.Close();
                    return;
                }

                // ---------- static files ----------
                var path = ctx.Request.Url?.AbsolutePath ?? "/";
                if (path == "/") path = "/index.html";

                // Prevent traversal
                path = path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(_rootFull, path));

                if (!full.StartsWith(_rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    AddCorsHeaders(ctx.Response);
                    ctx.Response.StatusCode = 403;
                    ctx.Response.Close();
                    return;
                }

                if (!File.Exists(full))
                {
                    AddCorsHeaders(ctx.Response);
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                    return;
                }

                // Read / cache bytes (huge win if browser requests assets repeatedly)
                var cached = GetOrLoadCached(full);

                AddCorsHeaders(ctx.Response);

                // Allow caching for static assets (you can tune this)
                // This avoids re-reading files constantly.
                ctx.Response.Headers["Cache-Control"] = "public, max-age=3600";

                ctx.Response.ContentType = cached.Mime;
                ctx.Response.ContentLength64 = cached.Bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(cached.Bytes, 0, cached.Bytes.Length).ConfigureAwait(false);
                ctx.Response.Close();
            }
            catch
            {
                try
                {
                    AddCorsHeaders(ctx.Response);
                    ctx.Response.StatusCode = 500;
                    ctx.Response.Close();
                }
                catch { }
            }
        }

        private CachedFile GetOrLoadCached(string fullPath)
        {
            var lastWrite = File.GetLastWriteTimeUtc(fullPath);

            if (_fileCache.TryGetValue(fullPath, out var existing))
            {
                if (existing.LastWriteUtc == lastWrite)
                    return existing;
            }

            // load fresh
            byte[] bytes = File.ReadAllBytes(fullPath);
            string mime = GetMime(fullPath);
            var cached = new CachedFile(bytes, mime, lastWrite);
            _fileCache[fullPath] = cached;
            return cached;
        }

        private static void AddCorsHeaders(HttpListenerResponse res)
        {
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] =
                "Content-Type, Authorization, X-Requested-With";
            res.Headers["Access-Control-Max-Age"] = "600";
        }

        private static string GetMime(string file)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "text/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                _ => "application/octet-stream"
            };
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }

            try { _listener?.Stop(); } catch { }  // unblocks GetContextAsync
            try { _listener?.Close(); } catch { }

            if (_thread != null && _thread.IsAlive)
            {
                try { _thread.Join(500); } catch { }
            }

            try { _cts?.Dispose(); } catch { }
            _cts = null;
            _listener = null;
            _thread = null;
            _fileCache.Clear();

            while (_commandQueue.TryDequeue(out _)) { }
        }
    }
}
