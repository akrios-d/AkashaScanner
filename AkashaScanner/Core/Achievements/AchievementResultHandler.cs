using AkashaScanner.Core.DataFiles;
using AkashaScanner.Core.ResultHandler;

namespace AkashaScanner.Core.Achievements
{
    public class AchievementResultHandler : IResultHandler<Achievement>
    {
        private readonly IDataFileRepository<AchievementOutput> DataFileRepository;
        private readonly AchievementOutput Dict = new();

        public AchievementResultHandler(IDataFileRepository<AchievementOutput> dataFileRepository)
        {
            DataFileRepository = dataFileRepository;
        }

        public void Init()
        {
            Dict.Clear();
        }

        public void Add(Achievement item, int order)
        {
            Dict[item.Id] = item.CategoryId;
        }

        public void Save()
        {
            var file = DataFileRepository.Create(Dict.Count);
            file.Write(Dict);
        }
    }
}
