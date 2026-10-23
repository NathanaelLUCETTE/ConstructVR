using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConstructVR
{
    [System.Serializable]
    public class SharedModelFile
    {
        public string Id;
        public string Name;
        public string Author;
        public string Date;
        public List<BrickPlacement> Bricks = new List<BrickPlacement>();
    }

    /// <summary>
    /// Mode collaboratif : chaque joueur publie ses constructions comme modèles (un fichier JSON par modèle)
    /// dans un dossier partagé. Tous les modèles du dossier peuvent être construits en mode guidé ou en défi.
    /// Pour jouer à plusieurs, il suffit de partager ce dossier (clé USB, OneDrive, dossier réseau…).
    /// </summary>
    public static class SharedModels
    {
        public static readonly string[] PlayerNames =
            { "Alex", "Sam", "Léa", "Noah", "Jade", "Tom", "Inès", "Lucas", "Emma", "Hugo", "Lina", "Nathan" };

        public static string PlayerName => PlayerNames[Mathf.Abs(SaveSystem.Data.PlayerNameIndex) % PlayerNames.Length];

        /// <summary>Dossier partagé : « Documents/ConstructVR/Modèles partagés » sur PC, dossier de l'appli sur le casque.</summary>
        public static string Folder
        {
            get
            {
                string root = Application.platform == RuntimePlatform.Android
                    ? Application.persistentDataPath
                    : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "ConstructVR");
                return Path.Combine(root, "Modèles partagés");
            }
        }

        /// <summary>Tous les modèles du dossier, du plus récent au plus ancien.</summary>
        public static List<BuildModel> LoadAll()
        {
            var list = new List<BuildModel>();
            try
            {
                if (!Directory.Exists(Folder)) return list;
                var files = new List<FileInfo>(new DirectoryInfo(Folder).GetFiles("*.json"));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                foreach (var f in files)
                {
                    try
                    {
                        var data = JsonUtility.FromJson<SharedModelFile>(File.ReadAllText(f.FullName));
                        if (data == null || data.Bricks == null || data.Bricks.Count == 0) continue;
                        list.Add(ToModel(data));
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("Modèle partagé illisible (" + f.Name + ") : " + e.Message);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Impossible de lire les modèles partagés : " + e.Message);
            }
            return list;
        }

        /// <summary>Publie une construction comme modèle. Renvoie null si l'enregistrement échoue.</summary>
        public static BuildModel Publish(List<BrickPlacement> bricks)
        {
            var data = new SharedModelFile
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 10),
                Author = PlayerName,
                Date = System.DateTime.Now.ToString("dd/MM HH:mm"),
                Bricks = new List<BrickPlacement>(bricks),
            };
            int count = 1;
            foreach (var m in LoadAll()) if (m.Author == data.Author) count++;
            data.Name = "Création de " + data.Author + " n°" + count;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, data.Author + "_" + data.Id + ".json"), JsonUtility.ToJson(data, true));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Impossible de publier le modèle : " + e.Message);
                return null;
            }
            return ToModel(data);
        }

        static BuildModel ToModel(SharedModelFile data)
        {
            int n = data.Bricks.Count;
            var m = new BuildModel
            {
                Id = "communaute_" + data.Id,
                Name = data.Name,
                Author = string.IsNullOrEmpty(data.Author) ? "?" : data.Author,
                Difficulty = n <= 12 ? Difficulty.Facile : n <= 25 ? Difficulty.Moyen : n <= 60 ? Difficulty.Difficile : Difficulty.Extreme,
                Celebration = Celebration.Spin,
                Steps = new List<BrickPlacement>(data.Bricks),
            };
            LegoCatalog.SortByLevel(m); // en mode guidé, chaque brique doit avoir un support
            return m;
        }
    }
}
