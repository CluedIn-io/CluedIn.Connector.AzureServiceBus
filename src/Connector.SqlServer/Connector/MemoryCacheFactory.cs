using Microsoft.Extensions.Caching.Memory;

namespace CluedIn.Connector.AzureServiceBus.Connector
{
    public interface IMemoryCacheFactory
    {
        IMemoryCache Create(MemoryCacheOptions memoryCacheOptions);
    }

    internal class MemoryCacheFactory : IMemoryCacheFactory
    {
        public IMemoryCache Create(MemoryCacheOptions memoryCacheOptions)
        {
            return new MemoryCache(memoryCacheOptions);
        }
    }
}
