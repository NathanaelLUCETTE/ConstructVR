using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;

namespace ConstructVR
{
    /// <summary>
    /// Le simulateur XR (clavier/souris) remplace le casque dès qu'il est actif : il retire le vrai casque
    /// des périphériques et la caméra ne suit plus la tête. Il est donc désactivé dans la scène,
    /// et on ne le réactive que si aucun casque n'est branché.
    /// </summary>
    public static class HeadsetDetector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (IsHeadsetConnected())
            {
                Debug.Log("ConstructVR : casque détecté, simulateur XR désactivé.");
                return;
            }

            foreach (var sim in Object.FindObjectsByType<XRInteractionSimulator>(FindObjectsInactive.Include))
                sim.gameObject.SetActive(true);
            Debug.Log("ConstructVR : pas de casque, simulateur XR activé (clavier/souris).");
        }

        static bool IsHeadsetConnected()
        {
            var settings = XRGeneralSettings.Instance;
            return settings != null && settings.Manager != null && settings.Manager.activeLoader != null;
        }
    }
}
