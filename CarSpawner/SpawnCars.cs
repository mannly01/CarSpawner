using Il2Cpp;
using Il2CppCMS.Core;
using Il2CppCMS.Core.Car;
using Il2CppCMS.UI;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(CarSpawner.SpawnCars), "CarSpawner", "1.0.1", "mannly01", "https://www.nexusmods.com/carmechanicsimulator2027/mods/21")]
[assembly: MelonGame("Red Dot Games", "Car Mechanic Simulator 2027 Demo")]

namespace CarSpawner
{
    /// <summary>
    /// Create a "CarSpawner.cfg" file in the Mods folder.
    /// </summary>
    public class CarSpawnerConfigFile
    {
        /// <summary>
        /// Settings Category
        /// </summary>
        private const string SettingsCatName = "CarSpawnerSettings";
        private readonly MelonPreferences_Category _settings;

        /// <summary>
        /// User setting for the key to open spawn the cars.
        /// </summary>
        public KeyCode SpawnCars => _spawnCars.Value;
        private readonly MelonPreferences_Entry<KeyCode> _spawnCars;

        public CarSpawnerConfigFile()
        {
#if DEBUG
            MelonLogger.Msg("ConfigFile.Init: Called");
#endif
            _settings = MelonPreferences.CreateCategory(SettingsCatName);
            _settings.SetFilePath("Mods/CarSpawner.cfg");

            _spawnCars = _settings.CreateEntry(nameof(SpawnCars), KeyCode.F3,
                description: "https://docs.unity3d.com/ScriptReference/KeyCode.html for KeyCodes.\nPress this key to spawn the cars. Default: F3");

            try
            {
                if (!File.Exists($"{Directory.GetCurrentDirectory()}\\Mods\\CarSpawner.cfg"))
                {
                    _settings.SaveToFile();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"Unable to ensure config path: {ex.Message}", "ConfigFile.Init");
            }
        }
    }

    public class SpawnCars : MelonMod
    {
        /// <summary>
        /// Reference to Settings file.
        /// </summary>
        private CarSpawnerConfigFile _configFile;

        /// <summary>
        /// Global reference to the current scene.
        /// </summary>
        private string _currentScene = string.Empty;

        /// <summary>
        /// An array of the 10 vehicles with valid configs available in the demo.
        /// </summary>
        private static readonly (string CarID, string LocalizedName, ushort ConfigVersion)[] _spawnVehicles =
        [
            ("car_boltnevada", "Bolt Nevada", 0),
            ("car_boltnevada", "Bolt Nevada", 3),
            ("car_boltnevada", "Bolt Nevada", 4),
            ("car_dnbcensor", "DNB Censor", 0),
            ("car_dnbcensor", "DNB Censor", 3),
            ("car_katagiritamagobp", "Katagiri Tamago BP", 0),
            ("car_lacroixmodiquer", "Lacroix Modique R", 2),
            ("car_luxorstreamlinermk3", "Luxor Streamliner Mk3", 0),
            ("car_mayenm5", "Mayen M5", 1),
            ("car_salemariesmk3", "Salem Aries MK3", 0)
        ];

        public override void OnInitializeMelon()
        {
            // Tell the user that we're loading the Settings.
            MelonLogger.Msg("Loading Settings...");
            _configFile = new CarSpawnerConfigFile();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == -1)
            {
                return;
            }
            // Save a reference to the current scene.
            _currentScene = sceneName.ToLower();
            if (_currentScene.Equals("christmas") ||
                _currentScene.Equals("easter") ||
                _currentScene.Equals("halloween"))
            {
                MelonLogger.Msg($"{sceneName} custom scene is active");
                _currentScene = "garage";
            }
#if DEBUG
            if (_currentScene.Equals("garage") ||
                _currentScene.Equals("barn") ||
                _currentScene.Equals("junkyard"))
            {
                MelonLogger.Msg($"SceneName: {_currentScene}");
            }
#endif
        }

        public override void OnUpdate()
        {
            // If the Config File was not initialized, fail
            if (_configFile == null)
                return;

#if DEBUG
            if (Input.GetKeyUp(KeyCode.J))
            {
                // This is a test key that should not be shipped with a release.
                MelonLogger.Msg("DEBUG Key (J) Pressed.");
            }
#endif

            if (Input.GetKeyUp(_configFile.SpawnCars))
            {
                if (!CheckIfInputIsFocused())
                {
                    if (_currentScene.Equals("garage"))
                    {
                        if (GameState.CurrentNetworkMode == GameNetworkMode.Client)
                        {
                            MelonLogger.Msg("User is a multiplayer client");
                            UIManager.Get().ShowInfoWindow("You are in a multiplayer session.\n\nThis feature is disabled for the client.");
                        }
                        else
                        {
                            VerifySpawnedCars();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if there are cars spawned and asks the user to continue.
        /// </summary>
        public static void VerifySpawnedCars()
        {
            var carLoadersGO = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "CarLoaders");
            if (carLoadersGO != null)
            {
                int spawnedCars = 0;
                var carLoaders = carLoadersGO.GetComponentsInChildren<CarLoader>();
                // Get a count of actual spawned cars to tell the user.
                // The loop after this, fills in any vehicle at any index.
                foreach (var loader in carLoaders)
                {
                    if (!string.IsNullOrWhiteSpace(loader.CarID))
                    {
                        spawnedCars++;
                    }
                }
#if DEBUG
                MelonLogger.Msg($"Spawned Cars: {spawnedCars}, Car Loaders: {carLoaders.Length}"); 
#endif
                if (spawnedCars > 0)
                {
                    // Show an Ask Window to tell the user their cars will be deleted.
                    var confirmSpawn = new Action<bool>(response =>
                    {
                        if (response)
                        {
                            for (int i = 0; i < carLoaders.Length; i++)
                            {
                                var carLoader = carLoaders[i];
                                if (!string.IsNullOrWhiteSpace(carLoader.CarID))
                                {
                                    var carDebug = carLoader.GetComponent<CarDebug>();
                                    carDebug?.DeleteCar();
                                    spawnedCars--;
                                }
                            }
                            MelonCoroutines.Start(SpawnCarConfigs());
                            GameMode.Get().SetCurrentMode(gameMode.Garage);
                        }
                        else
                        {
                            MelonLogger.Msg("User cancelled the Car Spawn.");
                            GameMode.Get().SetCurrentMode(gameMode.Garage);
                            return;
                        }
                    });
                    UIManager.Get().ShowAskWindow("Spawn Cars?", "This spawns all 10 cars available.\n\nIT WILL DELETE ALL CARS IN THE GAME.\n\nAre you sure you want to continue?", confirmSpawn);
                    GameMode.Get().SetCurrentMode(gameMode.UI);
                }
                else
                {
                    MelonCoroutines.Start(SpawnCarConfigs());
                }
            }
        }
        /// <summary>
        /// Coroutine to spawn all available car configs.
        /// </summary>
        public static System.Collections.IEnumerator SpawnCarConfigs()
        {
            var carLoadersGO = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == "CarLoaders");
            if (carLoadersGO != null)
            {
                var carLoaders = carLoadersGO.GetComponentsInChildren<CarLoader>();
                // Loop through the loaders and spawn a car.
                for (int i = 0; i < carLoaders.Length; i++)
                {
                    MelonLogger.Msg($"Car Loader {i + 1} Start");
                    var carLoader = carLoaders[i];
                    if (string.IsNullOrWhiteSpace(carLoader.CarID))
                    {
                        var carDebug = carLoader.GetComponent<CarDebug>();
                        if (carDebug != null)
                        {
                            (string CarId, string CarName, ushort ConfigVersion) = _spawnVehicles[i];
                            carDebug.LoadCar(CarId, ConfigVersion);
                            while (!carLoader.done)
                            {
                                yield return new WaitForSeconds(0.1f);
                            }
                            yield return new WaitForFixedUpdate();
                            yield return new WaitForEndOfFrame();

                            if (!string.IsNullOrWhiteSpace(carLoader.CarID))
                            {
#if DEBUG
                                MelonLogger.Msg($"Car {i + 1}: {CarName}, Config: {ConfigVersion}");
#endif
                                if (i < 3)
                                {
                                    //carLoader.MoveToCarPlaceInstant(CarPlace.AdditionalGarageParking);
                                    switch (i)
                                    {
                                        case 0:
                                            carLoader.ChangePosition(CarPlace.CarLifter1);
                                            break;
                                        case 1:
                                            carLoader.ChangePosition(CarPlace.Entrance2);
                                            break;
                                        case 2:
                                            carLoader.ChangePosition(CarPlace.Entrance3);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                carLoader.ShowNewCarWaitingPopup = true;
                                Il2CppCMS.UI.Markers.MapMarkersManager markersManager = Il2CppCMS.SceneLoaders.GarageLoader.Get().MapMarkersManager;
                                markersManager.RefreshMarkers();
                                carLoader.PinMarker(true);
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("Car Debug script is null.");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg($"CarLoader {i + 1} is not empty");
                    }
                }
                SoundManager.Get().PlaySFX("TutorialBing");
                MelonLogger.Msg($"Spawned All Available Vehicles");
            }

            yield return null;
        }

        /// <summary>
        /// If the user is using the Search box,
        /// the mod should do nothing.
        /// </summary>
        /// <returns>(bool) True if the Search/Input Field is being used.</returns>
        private static bool CheckIfInputIsFocused()
        {
            var evt = EventSystem.current;
            if (evt != null)
            {
                var activeGO = evt.currentSelectedGameObject;
                if (activeGO != null)
                {
                    // Check for legacy InputField
                    if (activeGO.GetComponent<InputField>() != null)
                    {
                        if (activeGO.GetComponent<InputField>()?.isFocused == true)
                        {
                            return true;
                        }
                    }

                    // If TMP is used, it often exposes TMP_InputField type; do a safer type-name check
                    var tmpComp = activeGO.GetComponent("TMP_InputField");
                    if (tmpComp != null)
                    {
                        // assume TMP input is focused when selected
                        return true;
                    }
                }
            }

            return false;
        }
    }
}