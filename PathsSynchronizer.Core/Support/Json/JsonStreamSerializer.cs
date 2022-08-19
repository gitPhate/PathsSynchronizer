using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Support.Json
{
    public class JsonStreamSerializer
    {
        public static async Task SerializeAsync(object value, Stream s)
        {
            await JsonSerializer.SerializeAsync(s, value).ConfigureAwait(false);
            s.Seek(0, SeekOrigin.Begin);
        }

        public static async ValueTask<T?> DeserializeAsync<T>(Stream s)
        {
            return await JsonSerializer.DeserializeAsync<T>(s).ConfigureAwait(false);
        }
    }
}
