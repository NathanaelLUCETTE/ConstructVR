using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ConstructVR
{
    public enum GameMode { Menu, Tutorial, Free, Guided, Challenge }

    /// <summary>
    /// Cœur du jeu : menus, modes (Tutoriel, Construction libre, Modèle guidé, Défi),
    /// aide de Brico, records, sauvegardes et options.
    /// La scène est construite par le menu « ConstructVR > Construire la scène ».
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Ressources")]
        public LegoLibrary Library;

        [Header("Zone de jeu")]
        public BuildArea Area;
        public BrickPalette Palette;
        public Assistant Brico;
        public MusicPlayer Music;
        public BoxCollider TrashZone;
        public Transform ModelDisplay;
        public GameObject ModelDisplayRoot;
        public TextMeshPro ModelDisplayLabel;
        public Transform ConfettiPoint;

        [Header("Panneaux")]
        public GameObject MainMenu;
        public GameObject ModelSelect;
        public GameObject CreationsPanel;
        public GameObject OptionsPanel;
        public GameObject EndPanel;
        public GameObject Hud;

        [Header("Menu principal")]
        public GameObject ResumeButton;
        public TextMeshProUGUI MenuHint;

        [Header("Choix du modèle")]
        public TextMeshProUGUI ModelSelectTitle;
        public TextMeshProUGUI[] ModelCardInfo;
        public TextMeshProUGUI[] ModelCardRecord;
        public Image[] ModelCardStars; // 3 étoiles par carte, à la suite

        [Header("Mes créations")]
        public TextMeshProUGUI[] SlotLabels;
        public GameObject[] SlotSaveButtons;
        public GameObject[] SlotLoadButtons;
        public TextMeshProUGUI CreationsHint;

        [Header("Options")]
        public TextMeshProUGUI MusicValue;
        public TextMeshProUGUI EffectsValue;

        [Header("Bilan")]
        public TextMeshProUGUI EndTitle;
        public TextMeshProUGUI EndBody;
        public TextMeshProUGUI EndRecord;
        public Image[] EndStars;
        public GameObject EndNextButton;
        public GameObject EndReplayButton;
        public GameObject EndFreeButton;

        [Header("Bandeau d'infos")]
        public TextMeshProUGUI HudTitle;
        public TextMeshProUGUI HudInfo;
        public GameObject HudFreeButtons;
        public GameObject HudChallengeButtons;
        public GameObject HudTutorialButtons;

        [Header("Réglages")]
        public float ChallengeRevealTime = 30f;
        public float HintDelay = 20f;
        public float ShowDelay = 35f;

        public static readonly Color StarOn = new Color(1f, 0.78f, 0.1f);
        public static readonly Color StarOff = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color GhostWhite = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color GhostGreen = new Color(0.2f, 1f, 0.35f, 0.45f);
        static readonly Color GhostTarget = new Color(0.25f, 0.6f, 1f, 0.45f);
        static readonly Color GhostRed = new Color(1f, 0.15f, 0.1f, 0.6f);

        public XRInteractionManager InteractionManager { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.Menu;

        BuildModel m_Model;
        GameMode m_PendingMode;
        int m_Step;
        int m_Errors;
        int m_ConsecutiveErrors;
        float m_StartTime;
        float m_StepStartTime;
        int m_HintLevel;
        float m_RevealEnd;
        bool m_Running;
        int m_TipIndex;
        int m_TutorialStep;
        GameObject m_TargetGhost;
        MeshRenderer m_TargetGhostRenderer;
        Bounds m_TrashBounds;
        readonly HashSet<LegoBrick> m_Held = new HashSet<LegoBrick>();

        InputAction m_HelpAction;
        InputAction m_MenuAction;

        // ------------------------------------------------------------------ démarrage

        void Awake()
        {
            Instance = this;
            BrickFactory.Init(Library);
            Effects.Init(Library.ParticleMaterial);
            InteractionManager = FindAnyObjectByType<XRInteractionManager>();
            if (InteractionManager == null) InteractionManager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            m_TrashBounds = TrashZone.bounds;
            TrashZone.enabled = false;
            SoundFX.Volume = SaveSystem.Data.EffectsVolume;

            foreach (var action in GetComponentsInChildren<ButtonAction>(true))
            {
                var a = action;
                var button = a.GetComponent<Button>();
                button.onClick.AddListener(() => SoundFX.Play(SoundFX.Blip, a.transform.position, 0.5f));
                button.onClick.AddListener(() => HandleAction(a.Action, a.Argument));
            }
        }

        void OnEnable()
        {
            m_HelpAction = new InputAction("Aide", InputActionType.Button);
            m_HelpAction.AddBinding("<XRController>{RightHand}/primaryButton");
            m_HelpAction.AddBinding("<Keyboard>/h");
            m_HelpAction.performed += _ => OnHelpPressed();
            m_HelpAction.Enable();

            m_MenuAction = new InputAction("Menu", InputActionType.Button);
            m_MenuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            m_MenuAction.AddBinding("<Keyboard>/m");
            m_MenuAction.performed += _ => ShowPanel(MainMenu);
            m_MenuAction.Enable();
        }

        void OnDisable()
        {
            m_HelpAction?.Dispose();
            m_MenuAction?.Dispose();
        }

        void Start()
        {
            ShowPanel(MainMenu);
            ShowModel(null);
            ClearTargetGhost();
            Brico.Wave();
            if (!SaveSystem.Data.TutorialDone)
            {
                Brico.Say("Salut ! Moi c'est Brico, ton assistant de construction !");
                Brico.Say("C'est ta première fois ici ? Commence par le Tutoriel : je vais tout t'apprendre !", true);
            }
            else
            {
                Brico.Say("Re-salut ! Content de te revoir. On construit quoi aujourd'hui ?");
            }
        }

        // ------------------------------------------------------------------ boutons

        void HandleAction(string action, string arg)
        {
            switch (action)
            {
                case "tutorial": StartMode(GameMode.Tutorial); break;
                case "free": StartMode(GameMode.Free); break;
                case "guided": OpenModelSelect(GameMode.Guided); break;
                case "challenge": OpenModelSelect(GameMode.Challenge); break;
                case "model": StartMode(m_PendingMode, LegoCatalog.FindModel(arg)); break;
                case "creations": OpenCreations(); break;
                case "options": OpenOptions(); break;
                case "menu":
                case "back": ShowPanel(MainMenu); break;
                case "resume": ShowPanel(null); break;
                case "replay": StartMode(Mode, m_Model); break;
                case "next": StartMode(Mode, NextModel()); break;
                case "undo": Undo(); break;
                case "clear": Area.Clear(); Brico.Say("Hop, la plaque est toute propre !"); break;
                case "finish": FinishChallenge(); break;
                case "skip": FinishTutorial(); break;
                case "save": SaveCreation(int.Parse(arg)); break;
                case "load": LoadCreation(int.Parse(arg)); break;
                case "music+": ChangeVolume(true, 0.1f); break;
                case "music-": ChangeVolume(true, -0.1f); break;
                case "sfx+": ChangeVolume(false, 0.1f); break;
                case "sfx-": ChangeVolume(false, -0.1f); break;
                case "help": OnHelpPressed(); break;
            }
        }

        void ShowPanel(GameObject panel)
        {
            MainMenu.SetActive(panel == MainMenu);
            ModelSelect.SetActive(panel == ModelSelect);
            CreationsPanel.SetActive(panel == CreationsPanel);
            OptionsPanel.SetActive(panel == OptionsPanel);
            EndPanel.SetActive(panel == EndPanel);
            ResumeButton.SetActive(m_Running);
            MenuHint.text = SaveSystem.Data.TutorialDone ? "Vise un bouton avec ta manette et appuie sur la gâchette" : "Première fois ? Commence par le Tutoriel !";
        }

        void OpenModelSelect(GameMode mode)
        {
            m_PendingMode = mode;
            ModelSelectTitle.text = mode == GameMode.Guided ? "Modèle guidé : choisis ta construction" : "Défi : choisis ta construction";
            var models = LegoCatalog.Models;
            for (int i = 0; i < models.Length && i < ModelCardInfo.Length; i++)
            {
                var m = models[i];
                var rec = SaveSystem.Record(m.Id);
                ModelCardInfo[i].text = m.Difficulty + " · " + m.Steps.Count + " briques";
                int stars;
                if (mode == GameMode.Guided)
                {
                    stars = rec.GuidedStars;
                    ModelCardRecord[i].text = rec.GuidedBestTime < 0f ? "Pas encore réussi" :
                        "Record : " + FormatTime(rec.GuidedBestTime) + " · " + Plural(rec.GuidedBestErrors, "erreur");
                }
                else
                {
                    stars = rec.ChallengeStars;
                    ModelCardRecord[i].text = rec.ChallengeBestPrecision < 0 ? "Pas encore tenté" : "Record : " + rec.ChallengeBestPrecision + " %";
                }
                for (int s = 0; s < 3; s++) ModelCardStars[i * 3 + s].color = s < stars ? StarOn : StarOff;
            }
            ShowPanel(ModelSelect);
        }

        void OpenCreations()
        {
            bool canSave = m_Running && (Mode == GameMode.Free || Mode == GameMode.Tutorial);
            for (int i = 0; i < SaveSystem.CreationSlots; i++)
            {
                var c = SaveSystem.Data.Creations[i];
                SlotLabels[i].text = c.Used
                    ? "<b>Création " + (i + 1) + "</b>\n" + c.Bricks.Count + " briques · " + c.Date
                    : "<b>Création " + (i + 1) + "</b>\n<color=#FFFFFF88>Emplacement vide</color>";
                SlotSaveButtons[i].SetActive(canSave);
                SlotLoadButtons[i].SetActive(c.Used);
            }
            CreationsHint.text = canSave
                ? "Enregistre ta construction actuelle ou recharge une ancienne création."
                : "Pour enregistrer, lance la Construction libre puis reviens ici.";
            ShowPanel(CreationsPanel);
        }

        void OpenOptions()
        {
            RefreshOptions();
            ShowPanel(OptionsPanel);
        }

        void ChangeVolume(bool music, float delta)
        {
            var d = SaveSystem.Data;
            if (music) d.MusicVolume = Mathf.Clamp01(Mathf.Round((d.MusicVolume + delta) * 10f) / 10f);
            else d.EffectsVolume = Mathf.Clamp01(Mathf.Round((d.EffectsVolume + delta) * 10f) / 10f);
            Music.Volume = d.MusicVolume;
            SoundFX.Volume = d.EffectsVolume;
            SaveSystem.Save();
            RefreshOptions();
        }

        void RefreshOptions()
        {
            MusicValue.text = Mathf.RoundToInt(SaveSystem.Data.MusicVolume * 100f) + " %";
            EffectsValue.text = Mathf.RoundToInt(SaveSystem.Data.EffectsVolume * 100f) + " %";
        }

        // ------------------------------------------------------------------ modes

        void StartMode(GameMode mode, BuildModel model = null)
        {
            ShowPanel(null);
            Area.Clear();
            Mode = mode;
            m_Model = model;
            m_Step = 0;
            m_Errors = 0;
            m_ConsecutiveErrors = 0;
            m_StartTime = Time.time;
            m_RevealEnd = 0f;
            m_Running = true;
            Brico.StopShowing();
            Palette.Highlight(-1, -1);
            ClearTargetGhost();
            HudFreeButtons.SetActive(mode == GameMode.Free);
            HudChallengeButtons.SetActive(mode == GameMode.Challenge);
            HudTutorialButtons.SetActive(mode == GameMode.Tutorial);
            ShowModel(mode == GameMode.Guided || mode == GameMode.Challenge ? model : null);

            switch (mode)
            {
                case GameMode.Tutorial:
                    m_TutorialStep = 0;
                    Brico.Say("C'est parti pour le tutoriel ! Je vais t'apprendre à construire en 6 petites étapes.");
                    TutorialInstruction(true);
                    break;
                case GameMode.Free:
                    Brico.Say("Construction libre ! Construis tout ce que tu veux : les briques sont illimitées.");
                    break;
                case GameMode.Guided:
                    Brico.Say("On construit : " + model.Name + " ! Le modèle tourne sur la table, je te guide brique par brique.");
                    BeginStep();
                    break;
                case GameMode.Challenge:
                    m_RevealEnd = Time.time + ChallengeRevealTime;
                    Brico.Say("Regarde bien ce modèle : il disparaît dans " + Mathf.RoundToInt(ChallengeRevealTime) + " secondes ! Ensuite, reconstruis-le de mémoire.");
                    break;
            }
        }

        BuildModel NextModel()
        {
            var models = LegoCatalog.Models;
            for (int i = 0; i < models.Length; i++)
                if (models[i] == m_Model) return models[(i + 1) % models.Length];
            return models[0];
        }

        void ShowModel(BuildModel model)
        {
            foreach (Transform c in ModelDisplay) Destroy(c.gameObject);
            ModelDisplayRoot.SetActive(model != null);
            if (model == null) return;

            var holder = new GameObject("Pièces").transform;
            holder.SetParent(ModelDisplay, false);
            var bounds = new Bounds();
            bool first = true;
            foreach (var s in model.Steps)
            {
                var v = BrickFactory.CreateVisual(s.Shape, BrickFactory.GetMaterial(s.Color), holder);
                v.transform.localPosition = BrickFactory.LocalPosition(s);
                v.transform.localRotation = BrickFactory.LocalRotation(s.Rot);
                var shape = LegoCatalog.Shapes[s.Shape];
                var f = shape.Footprint(s.Rot);
                var b = new Bounds(v.transform.localPosition + Vector3.up * shape.Height * 0.5f,
                    new Vector3(f.x * LegoDims.Pitch, shape.Height, f.y * LegoDims.Pitch));
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }
            holder.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            ModelDisplayLabel.text = model.Name;
        }

        void Update()
        {
            if (ModelDisplayRoot.activeSelf)
                ModelDisplay.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);

            if (m_Running && Mode == GameMode.Challenge && m_RevealEnd > 0f && Time.time > m_RevealEnd)
            {
                m_RevealEnd = 0f;
                ShowModel(null);
                SoundFX.Play(SoundFX.Pop, ModelDisplay.position, 0.8f);
                Brico.Say("Pouf, le modèle a disparu ! À toi de jouer. Appuie sur « J'ai fini » quand ta construction est prête.");
            }

            if (m_Running && Mode == GameMode.Guided) UpdateGuidedHelp();
            UpdateHud();
        }

        void UpdateHud()
        {
            Hud.SetActive(m_Running);
            if (!m_Running) return;
            string time = FormatTime(Time.time - m_StartTime);
            switch (Mode)
            {
                case GameMode.Tutorial:
                    HudTitle.text = "Tutoriel — étape " + Mathf.Min(m_TutorialStep + 1, TutorialSteps.Length) + " / " + TutorialSteps.Length;
                    HudInfo.text = TutorialSteps[Mathf.Min(m_TutorialStep, TutorialSteps.Length - 1)];
                    break;
                case GameMode.Free:
                    HudTitle.text = "Construction libre";
                    HudInfo.text = Plural(Area.PlacedBricks.Count, "brique") + " posée" + (Area.PlacedBricks.Count > 1 ? "s" : "") + "   ·   " + time;
                    break;
                case GameMode.Guided:
                    HudTitle.text = m_Model.Name + " — étape " + Mathf.Min(m_Step + 1, m_Model.Steps.Count) + " / " + m_Model.Steps.Count;
                    HudInfo.text = (m_Step < m_Model.Steps.Count ? "Prends une " + LegoCatalog.Describe(CurrentStep.Shape, CurrentStep.Color) + "\n" : "") +
                                   time + "   ·   " + Plural(m_Errors, "erreur");
                    break;
                case GameMode.Challenge:
                    HudTitle.text = "Défi : " + m_Model.Name;
                    HudInfo.text = m_RevealEnd > 0f
                        ? "Mémorise le modèle : " + Mathf.CeilToInt(m_RevealEnd - Time.time) + " s"
                        : time + "   ·   " + Plural(Area.PlacedBricks.Count, "brique");
                    break;
            }
        }

        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
        static string Plural(int n, string word) => n + " " + word + (n > 1 ? "s" : "");

        // ------------------------------------------------------------------ tutoriel

        static readonly string[] TutorialSteps =
        {
            "Tourne la tête à gauche, vise une brique du tableau et appuie sur la gâchette.",
            "Garde la gâchette appuyée et pose la brique sur la plaque verte.",
            "Choisis une autre couleur dans les pastilles sous le tableau.",
            "Prends une nouvelle brique et pose-la SUR la première.",
            "Reprends une brique et jette-la dans la poubelle rouge à droite.",
            "Appuie sur le bouton A de la manette droite pour m'appeler.",
        };

        static readonly string[] TutorialTalk =
        {
            "Étape 1 : tourne la tête vers la gauche. Tu vois le tableau des briques ? Vise une brique avec ta manette, comme un pistolet, et appuie sur la gâchette !",
            "Elle est dans ta main ! Garde la gâchette appuyée, amène-la au-dessus de la plaque verte et lâche : elle s'emboîte toute seule. Tourne le poignet pour l'orienter.",
            "Clac ! Bien joué ! Maintenant, change de couleur : vise une des pastilles rondes sous le tableau.",
            "Super ! Prends une nouvelle brique et pose-la par-dessus la première. Regarde la brique transparente : elle te montre où ta brique va s'emboîter.",
            "Parfait ! Pour supprimer une brique, reprends-la avec la gâchette et lâche-la dans la poubelle rouge à ta droite.",
            "Et dernière chose : quand tu as besoin d'aide, appuie sur le bouton A de ta manette droite. Essaie maintenant !",
        };

        void TutorialInstruction(bool queue) => Brico.Say(TutorialTalk[m_TutorialStep], queue);

        void TutorialEvent(int step)
        {
            if (!m_Running || Mode != GameMode.Tutorial || m_TutorialStep != step) return;
            m_TutorialStep++;
            SoundFX.Play(SoundFX.Star, Brico.transform.position, 0.6f);
            if (m_TutorialStep >= TutorialSteps.Length) FinishTutorial();
            else TutorialInstruction(false);
        }

        void FinishTutorial()
        {
            SaveSystem.Data.TutorialDone = true;
            SaveSystem.Save();
            Brico.Celebrate();
            Brico.Say("Bravo, tu sais tout ! Tu peux maintenant construire librement, suivre un modèle ou relever un défi.");
            EndGame("Tutoriel terminé !", "Tu sais prendre, poser, colorier et jeter des briques.\nÀ toi de jouer !", 3, "", false);
            EndFreeButton.SetActive(true);
        }

        // ------------------------------------------------------------------ mode guidé

        BrickPlacement CurrentStep => m_Model.Steps[m_Step];

        void BeginStep()
        {
            m_StepStartTime = Time.time;
            m_HintLevel = 0;
            m_ConsecutiveErrors = 0;
            Brico.StopShowing();
            var s = CurrentStep;
            Palette.Highlight(s.Shape, s.Color);
            ShowTargetGhost(s);
            Brico.Say("Étape " + (m_Step + 1) + " : prends une " + LegoCatalog.Describe(s.Shape, s.Color) +
                      " et pose-la sur la brique bleue transparente.", m_Step == 0);
        }

        void ShowTargetGhost(BrickPlacement s)
        {
            ClearTargetGhost();
            m_TargetGhost = BrickFactory.CreateVisual(s.Shape, BrickFactory.GetGhostMaterial(GhostTarget), Area.transform, "Brique fantôme");
            m_TargetGhost.transform.localPosition = BrickFactory.LocalPosition(s);
            m_TargetGhost.transform.localRotation = BrickFactory.LocalRotation(s.Rot);
            m_TargetGhost.transform.localScale = Vector3.one * 1.01f;
            m_TargetGhostRenderer = m_TargetGhost.GetComponent<MeshRenderer>();
            m_TargetGhostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void ClearTargetGhost()
        {
            if (m_TargetGhost != null) Destroy(m_TargetGhost);
            m_TargetGhost = null;
        }

        void UpdateGuidedHelp()
        {
            if (m_TargetGhost != null)
            {
                // la brique fantôme clignote plus fort quand Brico la montre
                float a = m_HintLevel >= 2
                    ? 0.25f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f))
                    : 0.3f + 0.15f * Mathf.Sin(Time.time * 3f);
                var c = GhostTarget;
                c.a = Mathf.Round(a * 20f) / 20f; // limite le nombre de matériaux créés
                m_TargetGhostRenderer.sharedMaterial = BrickFactory.GetGhostMaterial(c);
            }

            // le joueur galère : Brico intervient tout seul
            float stuck = Time.time - m_StepStartTime;
            if (m_HintLevel == 0 && (stuck > HintDelay || m_ConsecutiveErrors >= 2)) GiveHint(1);
            else if (m_HintLevel == 1 && (stuck > ShowDelay || m_ConsecutiveErrors >= 3)) GiveHint(2);
        }

        void GiveHint(int level)
        {
            m_HintLevel = Mathf.Max(m_HintLevel, level);
            var s = CurrentStep;
            if (level == 1)
            {
                bool holdsRight = false;
                foreach (var b in m_Held)
                    if (b != null && b.ShapeId == s.Shape && b.ColorId == s.Color) holdsRight = true;
                if (holdsRight)
                    Brico.Say("Tu as la bonne brique ! Amène-la sur la brique bleue transparente, tourne-la dans le même sens, puis lâche la gâchette.");
                else
                    Brico.Say("Regarde le tableau à gauche : la brique qui grossit, c'est la " + LegoCatalog.Shapes[s.Shape].Name +
                              ". Choisis d'abord la couleur " + LegoCatalog.Colors[s.Color].Name + " dans les pastilles, puis vise la brique.");
            }
            else
            {
                Brico.Say("Je te montre ! La " + LegoCatalog.Describe(s.Shape, s.Color) + " va exactement ici, là où je pointe.");
                Brico.ShowSpot(Area.WorldPosition(s) + Area.transform.up * LegoCatalog.Shapes[s.Shape].Height);
            }
        }

        void CompleteStep()
        {
            m_Step++;
            m_ConsecutiveErrors = 0;
            if (m_Step >= m_Model.Steps.Count)
            {
                ClearTargetGhost();
                Palette.Highlight(-1, -1);
                Brico.StopShowing();
                float time = Time.time - m_StartTime;
                int stars = m_Errors == 0 ? 3 : m_Errors <= 2 ? 2 : 1;
                var rec = SaveSystem.Record(m_Model.Id);
                bool record = rec.GuidedBestTime < 0f || time < rec.GuidedBestTime;
                if (record) rec.GuidedBestTime = time;
                if (rec.GuidedBestErrors < 0 || m_Errors < rec.GuidedBestErrors) rec.GuidedBestErrors = m_Errors;
                rec.GuidedStars = Mathf.Max(rec.GuidedStars, stars);
                SaveSystem.Save();
                EndGame(m_Model.Name + " terminée !",
                    "Temps : " + FormatTime(time) + "\n" + Plural(m_Errors, "erreur"),
                    stars, record ? "Nouveau record !" : "Record : " + FormatTime(rec.GuidedBestTime), true);
                Brico.Celebrate();
                Brico.Say(m_Errors == 0 ? "Incroyable, aucune erreur ! Tu es un vrai maître constructeur !" : "Super travail ! Tu as réussi la construction !");
                return;
            }
            string[] cheers = { "Bien joué !", "Parfait !", "Super !", "Exactement !", "Bravo !", "Trop fort !" };
            Brico.Say(cheers[Random.Range(0, cheers.Length)]);
            BeginStep();
        }

        // ------------------------------------------------------------------ défi

        void FinishChallenge()
        {
            if (!m_Running || Mode != GameMode.Challenge) return;
            if (m_RevealEnd > 0f) { Brico.Say("Attends que le modèle disparaisse avant de terminer !"); return; }
            int matched = 0;
            var used = new HashSet<LegoBrick>();
            foreach (var step in m_Model.Steps)
            foreach (var b in Area.PlacedBricks)
            {
                if (used.Contains(b) || !b.Placement.Matches(step)) continue;
                used.Add(b);
                matched++;
                break;
            }
            int extra = Area.PlacedBricks.Count - matched;
            int total = m_Model.Steps.Count;
            int precision = Mathf.RoundToInt(100f * matched / (total + extra));
            int stars = precision >= 100 ? 3 : precision >= 75 ? 2 : precision >= 40 ? 1 : 0;
            var rec = SaveSystem.Record(m_Model.Id);
            bool record = precision > rec.ChallengeBestPrecision;
            if (record) rec.ChallengeBestPrecision = precision;
            rec.ChallengeStars = Mathf.Max(rec.ChallengeStars, stars);
            SaveSystem.Save();

            ShowModel(m_Model);
            EndGame(precision >= 100 ? "Mémoire parfaite !" : precision >= 75 ? "Bien joué !" : "Pas mal !",
                "Briques bien placées : " + matched + " / " + total +
                (extra > 0 ? "\nBriques en trop ou mal placées : " + extra : "") +
                "\nPrécision : " + precision + " %   ·   Temps : " + FormatTime(Time.time - m_StartTime),
                stars, record ? "Nouveau record !" : "Record : " + rec.ChallengeBestPrecision + " %", true);
            if (precision >= 75) Brico.Celebrate();
            Brico.Say(precision >= 100 ? "Quelle mémoire ! C'est exactement le modèle !" : "Compare avec le modèle qui est revenu sur la table. Tu veux réessayer ?");
        }

        void EndGame(string title, string body, int stars, string record, bool canReplay)
        {
            m_Running = false;
            EndTitle.text = title;
            EndBody.text = body;
            EndRecord.text = record;
            EndReplayButton.SetActive(canReplay);
            EndNextButton.SetActive(canReplay);
            EndFreeButton.SetActive(false);
            ShowPanel(EndPanel);
            StartCoroutine(RevealStars(stars));
            Effects.Confetti(ConfettiPoint.position);
            SoundFX.Play(SoundFX.Success, EndPanel.transform.position, 0.8f);
        }

        IEnumerator RevealStars(int stars)
        {
            foreach (var s in EndStars) { s.color = StarOff; s.transform.localScale = Vector3.one; }
            for (int i = 0; i < stars && i < EndStars.Length; i++)
            {
                yield return new WaitForSeconds(0.35f);
                EndStars[i].color = StarOn;
                EndStars[i].transform.localScale = Vector3.one * 1.4f;
                SoundFX.Play(SoundFX.Star, EndStars[i].transform.position, 0.6f);
                float t = 0f;
                while (t < 0.2f)
                {
                    t += Time.deltaTime;
                    EndStars[i].transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, t / 0.2f);
                    yield return null;
                }
            }
        }

        // ------------------------------------------------------------------ créations

        void Undo()
        {
            var last = Area.LastPlaced;
            if (last == null) { Brico.Say("Il n'y a rien à annuler !"); return; }
            SoundFX.Play(SoundFX.Trash, last.transform.position, 0.5f);
            Area.Remove(last);
            Destroy(last.gameObject);
        }

        void SaveCreation(int slot)
        {
            SaveSystem.SaveCreation(slot, Area.GetPlacements());
            Brico.Say("C'est enregistré dans « Création " + (slot + 1) + " » !");
            SoundFX.Play(SoundFX.Star, Brico.transform.position, 0.6f);
            OpenCreations();
        }

        void LoadCreation(int slot)
        {
            var c = SaveSystem.Data.Creations[slot];
            if (!c.Used) return;
            StartMode(GameMode.Free);
            foreach (var p in c.Bricks)
            {
                var b = LegoBrick.Create(p.Shape, p.Color, Area.WorldPosition(p), Area.WorldRotation(p));
                Area.Place(b, p, false);
            }
            Brico.Say("Voici ta création " + (slot + 1) + " ! Tu peux continuer à la construire.");
        }

        // ------------------------------------------------------------------ événements des briques

        public void OnBrickGrabbed(LegoBrick brick) => m_Held.Add(brick);

        public void OnColorChosen(int color) => TutorialEvent(2);

        public void OnBrickTakenFromPalette(int shape, int color)
        {
            TutorialEvent(0);
            if (m_Running && Mode == GameMode.Guided && (shape != CurrentStep.Shape || color != CurrentStep.Color) && m_HintLevel == 0)
                Brico.Say("Attention, pour cette étape il faut une " + LegoCatalog.Describe(CurrentStep.Shape, CurrentStep.Color) + ".");
        }

        public bool TryGetPreview(LegoBrick brick, out BrickPlacement placement, out Color color)
        {
            color = GhostWhite;
            placement = default;
            if (InTrash(brick.transform.position)) return false;
            if (!Area.ComputePlacement(brick, out placement)) return false;
            if (m_Running && Mode == GameMode.Guided && CurrentStep.Matches(placement)) color = GhostGreen;
            return true;
        }

        public void OnBrickReleased(LegoBrick brick, XRBaseInputInteractor interactor)
        {
            m_Held.Remove(brick);

            if (InTrash(brick.transform.position))
            {
                SoundFX.Play(SoundFX.Trash, brick.transform.position, 0.7f);
                interactor?.SendHapticImpulse(0.3f, 0.1f);
                Destroy(brick.gameObject);
                TutorialEvent(4);
                return;
            }

            if (!Area.ComputePlacement(brick, out var placement))
            {
                brick.Drop();
                return;
            }

            if (m_Running && Mode == GameMode.Guided)
            {
                var step = CurrentStep;
                if (!step.Matches(placement))
                {
                    m_Errors++;
                    m_ConsecutiveErrors++;
                    bool rightBrick = brick.ShapeId == step.Shape && brick.ColorId == step.Color;
                    Brico.Say(rightBrick
                        ? "Bonne brique, mais pas au bon endroit ! Pose-la sur la brique bleue transparente."
                        : "Oups, ce n'est pas la bonne brique ! Il faut une " + LegoCatalog.Describe(step.Shape, step.Color) + ".");
                    SoundFX.Play(SoundFX.Error, brick.transform.position, 0.7f);
                    interactor?.SendHapticImpulse(0.8f, 0.25f);
                    StartCoroutine(FlashError(placement));
                    brick.Drop();
                    return;
                }
                Area.Place(brick, step);
                OnSnapped(brick, step, interactor);
                CompleteStep();
                return;
            }

            Area.Place(brick, placement);
            OnSnapped(brick, placement, interactor);
            TutorialEvent(1);
            if (placement.Level > 0) TutorialEvent(3);
        }

        void OnSnapped(LegoBrick brick, BrickPlacement p, XRBaseInputInteractor interactor)
        {
            var pos = Area.WorldPosition(p);
            SoundFX.Play(SoundFX.Click, pos, 1f);
            Effects.SnapPuff(pos);
            interactor?.SendHapticImpulse(0.5f, 0.06f);
        }

        IEnumerator FlashError(BrickPlacement p)
        {
            var red = BrickFactory.CreateVisual(p.Shape, BrickFactory.GetGhostMaterial(GhostRed), Area.transform, "Erreur");
            red.transform.localPosition = BrickFactory.LocalPosition(p);
            red.transform.localRotation = BrickFactory.LocalRotation(p.Rot);
            red.transform.localScale = Vector3.one * 1.04f;
            for (int i = 0; i < 3; i++)
            {
                red.SetActive(true);
                yield return new WaitForSeconds(0.18f);
                red.SetActive(false);
                yield return new WaitForSeconds(0.12f);
            }
            Destroy(red);
        }

        bool InTrash(Vector3 position) => m_TrashBounds.Contains(position);

        void OnHelpPressed()
        {
            if (m_Running && Mode == GameMode.Tutorial && m_TutorialStep == 5)
            {
                Brico.Wave();
                TutorialEvent(5);
                return;
            }
            if (!m_Running)
            {
                Brico.Wave();
                Brico.Say("Je suis là ! Choisis un mode dans le menu avec ta manette. Si le menu a disparu, appuie sur le bouton Menu de la manette gauche.");
                return;
            }
            switch (Mode)
            {
                case GameMode.Tutorial:
                    TutorialInstruction(false);
                    break;
                case GameMode.Guided:
                    GiveHint(Mathf.Min(m_HintLevel + 1, 2));
                    break;
                case GameMode.Challenge:
                    Brico.Say(m_RevealEnd > 0f ? "Profite que le modèle soit encore là pour bien le regarder !" : "Pas d'aide en mode Défi… mais je crois en toi !");
                    break;
                default:
                    string[] tips =
                    {
                        "Astuce : tu peux reprendre une brique déjà posée pour la déplacer.",
                        "Astuce : choisis la couleur sous le tableau avant de prendre une brique.",
                        "Astuce : les plaques sont trois fois moins hautes que les briques, parfait pour faire un toit !",
                        "Astuce : tourne autour de la table avec le joystick pour voir ta construction sous tous les angles.",
                        "Astuce : le bouton « Annuler » enlève la dernière brique posée.",
                        "Astuce : enregistre ta construction dans « Sauvegarder » pour la retrouver plus tard !",
                    };
                    Brico.Say(tips[m_TipIndex++ % tips.Length]);
                    break;
            }
        }
    }
}
