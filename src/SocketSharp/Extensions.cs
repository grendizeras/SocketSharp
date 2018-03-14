using Newtonsoft.Json;
using SocketSharp.Abstract;
using System.Text;
using System.Threading.Tasks;

namespace SocketSharp
{
    public static class Extensions
    {
        public static async Task<TOutput> RequestAsync<TInput,TOutput>(this IRequestChannel channel, TInput data)
        {
            var serialized = JsonConvert.SerializeObject(data);
            var response=await channel.RequestAsync(Encoding.UTF8.GetBytes(serialized));
            return JsonConvert.DeserializeObject<TOutput>(Encoding.UTF8.GetString(response));
        }
    }
}
