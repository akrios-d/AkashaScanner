namespace AkashaScanner.Core.Scrappers
{
    public interface IScrapper<C> : IDisposable where C : IBaseScrapConfig
    {
        bool Start(C config);
    }
}
