using UnityEngine;

namespace ConstructVR
{
    /// <summary>
    /// Maillages et matériaux des briques, générés une fois pour toutes par
    /// le menu « ConstructVR > Construire la scène » et enregistrés dans le projet.
    /// </summary>
    [CreateAssetMenu(menuName = "ConstructVR/Bibliothèque Lego")]
    public class LegoLibrary : ScriptableObject
    {
        public Mesh[] BrickMeshes;
        public Material[] ColorMaterials;
        public Material GhostMaterial;
        public Mesh BaseplateMesh;
        public Material ParticleMaterial;
    }
}
