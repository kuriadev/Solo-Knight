using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoloKnight
{
    public enum GameState { Menu, Playing, Paused, Victory, Defeat }

    /// <summary>
    /// Persistent (DontDestroyOnLoad) owner of the save data, scene flow and global state.
    /// A copy lives in every scene so any scene can be played directly from the editor;
    /// the first one to wake up survives, later copies destroy themselves.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioManager))]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Databases")]
        public ItemDatabaseSO itemDatabase;
        public FloorDatabaseSO floorDatabase;

        [Header("Shared Materials")]
        [Tooltip("Unlit material used as template for runtime VFX (telegraphs, rings, health bars).")]
        public Material unlitMaterial;

        [Header("Scenes")]
        public string mainMenuScene = "MainMenu";
        public string dungeonScene = "DungeonFloor_01";

        public SaveData Data { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;

        PlayerStats player;
        InventorySystem playerInventory;

        public PlayerStats Player => player;

        /// <summary>True whenever gameplay input (move/attack/interact) must be ignored.</summary>
        public bool IsGameplayBlocked =>
            State != GameState.Playing || (UIManager.Instance != null && UIManager.Instance.IsBlockingPanelOpen);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            MaterialUtil.SetUnlitTemplate(unlitMaterial);
            Data = SaveSystem.Load() ?? SaveData.CreateNew();
            Data.Validate();
            Application.targetFrameRate = 120;

            SceneManager.sceneLoaded += OnSceneLoaded;
            State = SceneManager.GetActiveScene().name == mainMenuScene ? GameState.Menu : GameState.Playing;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            player = null;
            playerInventory = null;
            State = scene.name == mainMenuScene ? GameState.Menu : GameState.Playing;
        }

        // ---------------- Player persistence ----------------

        public void RegisterPlayer(PlayerStats stats)
        {
            player = stats;
            playerInventory = stats.GetComponent<InventorySystem>();
            playerInventory.ImportFrom(Data, itemDatabase);
            stats.ImportFrom(Data);
        }

        public void CapturePlayerState()
        {
            if (player == null) return;
            player.ExportTo(Data);
            if (playerInventory != null) playerInventory.ExportTo(Data);
        }

        public void SaveGame()
        {
            CapturePlayerState();
            SaveSystem.Save(Data);
        }

        // ---------------- Flow ----------------

        public void NewGame()
        {
            SaveSystem.Delete();
            Data = SaveData.CreateNew();
            SaveSystem.Save(Data);
            LoadDungeon();
        }

        public void ContinueGame()
        {
            Data = SaveSystem.Load() ?? SaveData.CreateNew();
            LoadDungeon();
        }

        public void LoadDungeon()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(dungeonScene);
        }

        /// <summary>Restart the current floor, keeping level, stats, gold and gear.</summary>
        public void RetryFloor()
        {
            CapturePlayerState();
            SaveSystem.Save(Data);
            LoadDungeon();
        }

        public void AdvanceFloor()
        {
            CapturePlayerState();
            Data.currentFloor++;
            SaveSystem.Save(Data);
            AudioManager.Play(Sfx.Portal);
            LoadDungeon();
        }

        public void ReturnToMenu()
        {
            if (State != GameState.Menu) SaveGame();
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }

        public void QuitGame()
        {
            if (State != GameState.Menu) SaveGame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- Events from gameplay ----------------

        public void OnFloorCleared(int floor)
        {
            Data.highestFloorCleared = Mathf.Max(Data.highestFloorCleared, floor);
            SaveGame();
            State = GameState.Victory;
        }

        public void OnPlayerDied()
        {
            State = GameState.Defeat;
            SaveGame();
            if (UIManager.Instance != null) UIManager.Instance.ShowDefeat();
        }

        public void SetPaused(bool paused)
        {
            if (paused && State == GameState.Playing)
            {
                State = GameState.Paused;
                Time.timeScale = 0f;
            }
            else if (!paused && State == GameState.Paused)
            {
                State = GameState.Playing;
                Time.timeScale = 1f;
            }
        }

        /// <summary>Close the victory modal and keep playing on the cleared floor.</summary>
        public void ResumeAfterVictory()
        {
            if (State == GameState.Victory)
            {
                State = GameState.Playing;
                Time.timeScale = 1f;
            }
        }

        public void RecordKill()
        {
            Data.totalKills++;
        }

        void OnApplicationQuit()
        {
            if (State != GameState.Menu && player != null) SaveGame();
        }
    }
}
