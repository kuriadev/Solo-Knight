using UnityEngine;
using UnityEngine.UI;

namespace SoloKnight
{
    /// <summary>Title screen: New Game, Continue (if a save exists), Quit.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] Button newGameButton;
        [SerializeField] Text newGameLabel;
        [SerializeField] Button continueButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text saveInfoText;

        void Awake()
        {
            if (root == null) BuildUI();
            UIFactory.EnsureEventSystem();
            newGameButton.onClick.AddListener(() => { AudioManager.Play(Sfx.UiClick); if (GameManager.Instance != null) GameManager.Instance.NewGame(); });
            continueButton.onClick.AddListener(() => { AudioManager.Play(Sfx.UiClick); if (GameManager.Instance != null) GameManager.Instance.ContinueGame(); });
            quitButton.onClick.AddListener(() => { AudioManager.Play(Sfx.UiClick); if (GameManager.Instance != null) GameManager.Instance.QuitGame(); else Application.Quit(); });
        }

        void Start()
        {
            Time.timeScale = 1f;
            var save = SaveSystem.Load();
            continueButton.interactable = save != null;
            if (save != null)
            {
                saveInfoText.text = $"Saved game:  Floor {save.currentFloor}  ·  Level {save.level}  ·  {save.gold} Gold  ·  {save.totalKills} kills\n<size=16><color=#888888>Last saved {save.lastSaved}</color></size>";
                newGameLabel.text = "New Game  <size=15><color=#FF9A8A>(overwrites save)</color></size>";
            }
            else
            {
                saveInfoText.text = "<color=#888888>No saved game found.</color>";
                newGameLabel.text = "New Game";
            }
        }

        public void BuildUI()
        {
            UIFactory.ConfigureCanvas(gameObject, 0);
            UIFactory.ClearChildren(transform);
            root = UIFactory.Stretch(UIFactory.Rect("MainMenu", transform));

            var bg = UIFactory.Panel("Vignette", root, new Color(0f, 0f, 0f, 0.35f), false);
            UIFactory.Stretch(bg.rectTransform);

            var title = UIFactory.Label("Title", root, "SOLO KNIGHT", 110, TextAnchor.MiddleCenter, UIFactory.AccentColor, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1400f, 140f));
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.1f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            var sub = UIFactory.Label("Subtitle", root, "A real-time dungeon crawler  ·  Descend. Defeat. Grow stronger.", 26, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1400f, 40f));

            newGameButton = MenuButton("NewGame", "New Game", -40f, out newGameLabel);
            continueButton = MenuButton("Continue", "Continue", -120f, out _);
            quitButton = MenuButton("Quit", "Quit", -200f, out _);

            saveInfoText = UIFactory.Label("SaveInfo", root, "", 20, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(saveInfoText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(1000f, 70f));

            var controls = UIFactory.Label("Controls", root,
                "WASD / Arrows: Move   ·   Mouse: Aim   ·   LMB: Combo Attack   ·   RMB / Q: Whirlwind   ·   Space: Dash\nF: Interact   ·   1 / 2: Potions   ·   Tab / I: Character & Inventory   ·   Esc: Pause",
                17, TextAnchor.LowerCenter, new Color(0.7f, 0.7f, 0.75f));
            UIFactory.Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 60f));
        }

        Button MenuButton(string name, string label, float y, out Text text)
        {
            var b = UIFactory.Button(name, root, label, 28, UIFactory.ButtonColor, out text);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(460f, 64f));
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(UIFactory.AccentColor.r, UIFactory.AccentColor.g, UIFactory.AccentColor.b, 0.4f);
            o.effectDistance = new Vector2(2f, -2f);
            return b;
        }
    }
}
