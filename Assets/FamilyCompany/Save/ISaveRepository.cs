namespace FamilyCompany.Save
{
    public interface ISaveRepository
    {
        string Location { get; }
        void Save(GameSaveDto save);
        bool TryLoad(out GameSaveDto save);
    }
}

