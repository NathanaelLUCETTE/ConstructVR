using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConstructVR
{
    [System.Serializable]
    public class Creation
    {
        public bool Used;
        public string Date;
        public List<BrickPlacement> Bricks = new List<BrickPlacement>();
    }

    [System.Serializable]
    public class ModelRecord
    {
        public string ModelId;
        public float GuidedBestTime = -1f;
        public int GuidedBestErrors = -1;
        public int GuidedStars;
        public int ChallengeBestPrecision = -1;
        public int ChallengeStars;
    }

    [System.Serializable]
    public class SaveData
    {
        public List<Creation> Creations = new List<Creation>();
        public List<ModelRecord> Records = new List<ModelRecord>();
        public float MusicVolume = 0.35f;
        public float EffectsVolume = 0.8f;
        public bool TutorialDone;
        public int PlayerNameIndex;
    }

    /// <summary>Sauvegarde des créations, des records et des options (fichier JSON sur l'appareil).</summary>
    public static class SaveSystem
    {
        public const int CreationSlots = 3;

        static SaveData s_Data;

        static string FilePath => Path.Combine(Application.persistentDataPath, "constructvr_sauvegarde.json");

        public static SaveData Data
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        static void Load()
        {
            try
            {
                s_Data = File.Exists(FilePath) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Sauvegarde illisible, on repart de zéro : " + e.Message);
                s_Data = null;
            }
            if (s_Data == null) s_Data = new SaveData();
            while (s_Data.Creations.Count < CreationSlots) s_Data.Creations.Add(new Creation());
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Impossible d'enregistrer la sauvegarde : " + e.Message);
            }
        }

        public static ModelRecord Record(string modelId)
        {
            foreach (var r in Data.Records) if (r.ModelId == modelId) return r;
            var rec = new ModelRecord { ModelId = modelId };
            Data.Records.Add(rec);
            return rec;
        }

        public static void SaveCreation(int slot, List<BrickPlacement> bricks)
        {
            var c = Data.Creations[slot];
            c.Used = bricks.Count > 0;
            c.Date = System.DateTime.Now.ToString("dd/MM HH:mm");
            c.Bricks = new List<BrickPlacement>(bricks);
            Save();
        }
    }
}
