using System;
using System.IO;
using FamilyCompany.Save;
using UnityEngine;

namespace FamilyCompany.Infrastructure.Unity
{
    public sealed class UnityJsonSaveRepository : ISaveRepository
    {
        public const int MinimumSlot = 1;
        public const int MaximumSlot = 3;
        private const string LegacyFileName = "family-company-prototype-save.json";
        private readonly string _directory;

        public UnityJsonSaveRepository()
            : this(MinimumSlot)
        {
        }

        public UnityJsonSaveRepository(int slot, string directory = null)
        {
            if (slot < MinimumSlot || slot > MaximumSlot)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), $"Save slot must be {MinimumSlot}-{MaximumSlot}.");
            }

            Slot = slot;
            _directory = string.IsNullOrWhiteSpace(directory) ? Application.persistentDataPath : directory;
        }

        public int Slot { get; }
        public string Location => Path.Combine(_directory, $"family-company-save-slot-{Slot}.json");
        public bool Exists => ResolveReadableLocation() != null;
        public DateTime? LastWriteTimeLocal
        {
            get
            {
                var readable = ResolveReadableLocation();
                return readable == null ? null : File.GetLastWriteTime(readable);
            }
        }

        public void Save(GameSaveDto save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            var primary = Location;
            var temporary = primary + ".tmp";
            var backup = primary + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(primary));
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(primary))
            {
                File.Copy(primary, backup, true);
                File.Delete(primary);
            }

            File.Move(temporary, primary);
        }

        public bool TryLoad(out GameSaveDto save)
        {
            save = null;
            var readable = ResolveReadableLocation();
            if (readable == null)
            {
                return false;
            }

            var json = File.ReadAllText(readable);
            save = JsonUtility.FromJson<GameSaveDto>(json);
            return save != null;
        }

        private string ResolveReadableLocation()
        {
            if (File.Exists(Location)) return Location;
            if (Slot != MinimumSlot) return null;
            var legacy = Path.Combine(_directory, LegacyFileName);
            return File.Exists(legacy) ? legacy : null;
        }
    }
}
