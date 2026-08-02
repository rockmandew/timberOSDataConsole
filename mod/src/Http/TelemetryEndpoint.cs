using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Timberborn.HttpApiSystem;
using TimberOS.DataConsole.Telemetry;

namespace TimberOS.DataConsole.Http
{
    /// <summary>
    /// Serves timberOS telemetry on the game's native local HTTP server (default
    /// http://localhost:8080). Registered as an <see cref="IHttpApiEndpoint"/> so it
    /// shares the game's existing listener — no second web server, no extra ports.
    ///
    /// Runs on the HttpListener thread: it only reads the immutable, already-built
    /// snapshot from <see cref="SnapshotHolder"/> and never touches live game state.
    /// (The game's own WriteJson helper is internal, so we serialize with Newtonsoft
    /// directly — the same serializer the game uses.)
    ///
    ///   GET /timberos/v1/health    → { ok, hasSnapshot, sequence }
    ///   GET /timberos/v1/snapshot  → latest TelemetryEnvelope (503 until first collect)
    /// </summary>
    public sealed class TelemetryEndpoint : IHttpApiEndpoint
    {
        private static readonly Regex SnapshotPath = new Regex("^/timberos/v1/snapshot/?$", RegexOptions.Compiled);
        private static readonly Regex HealthPath = new Regex("^/timberos/v1/health/?$", RegexOptions.Compiled);

        private readonly SnapshotHolder _holder;

        public TelemetryEndpoint(SnapshotHolder holder)
        {
            _holder = holder;
        }

        public async Task<bool> TryHandle(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;

            if (HealthPath.IsMatch(path))
            {
                TelemetryEnvelope? current = _holder.Current;
                await WriteJson(context, new
                {
                    ok = true,
                    schemaVersion = "1.2.0",
                    hasSnapshot = current != null,
                    sequence = current?.Sequence
                });
                return true;
            }

            if (SnapshotPath.IsMatch(path))
            {
                TelemetryEnvelope? current = _holder.Current;
                if (current == null)
                {
                    await WriteText(context, "No snapshot collected yet. Load a settlement and retry.", 503);
                }
                else
                {
                    await WriteJson(context, current);
                }
                return true;
            }

            return false;
        }

        private static Task WriteJson(HttpListenerContext context, object payload)
        {
            string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            return Write(context, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json), 200);
        }

        private static Task WriteText(HttpListenerContext context, string text, int statusCode)
        {
            return Write(context, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text), statusCode);
        }

        private static async Task Write(HttpListenerContext context, string contentType, byte[] bytes, int statusCode)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            // The HttpApi closes the OutputStream after TryHandle returns; we only write.
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
