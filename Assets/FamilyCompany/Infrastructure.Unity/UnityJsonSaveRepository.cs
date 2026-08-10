using System;
using System.IO;
using FamilyCompany.Save;
using UnityEngine;

namespace FamilyCompany.Infrastructure.Unity
{
    public sealed class UnityJsonSaveRepository : ISaveRepository
    {
        private const string FileName = "family-company-prototype-save.json";

        public string Location => Path.Combine(Application.persistentDataPath, FileName);

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
            if (!File.Exists(Location))
            {
                return false;
            }

            var json = File.ReadAllText(Location);
            save = JsonUtility.FromJson<GameSaveDto>(json);
            return save != null;
        }
    }
}

