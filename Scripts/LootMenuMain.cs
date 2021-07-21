using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System.IO;


namespace LootMenuMod
{
    public class LootMenuMain : MonoBehaviour
    {
        Camera mainCamera;
        DaggerfallUI daggerfallUI;

        Texture2D backgroundTexture; 

        string backgroundTextureName = "background";

        private GUIStyle guiStyle = new GUIStyle();
        private float UIScale;
        float uiScaleModifier = 1;
        int playerLayerMask = 0;
        const float RayDistance = 32f;
        const float DefaultActivationDistance = 3.2f;
        bool enableLootMenu = false;

        void Awake()
        {
            backgroundTexture = DaggerfallUI.GetTextureFromResources(backgroundTextureName);
            backgroundTexture.filterMode = FilterMode.Point;
        }
        
        void Start()
        {
            daggerfallUI = GameObject.Find("DaggerfallUI").GetComponent<DaggerfallUI>();
            guiStyle.alignment = TextAnchor.LowerCenter;

            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            mainCamera = GameManager.Instance.MainCamera;
        }
        void LateUpdate()
        {
            UIScale = daggerfallUI.DaggerfallHUD.HUDVitals.Scale.x * uiScaleModifier;
            guiStyle.fontSize = (int)(10.0f * UIScale);
        }
        void Update()
        {
            if (GameManager.Instance.IsPlayerOnHUD)
            {
                DaggerfallLoot loot;
                Ray ray = new Ray();

                ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

                RaycastHit hit;
                bool hitSomething = Physics.Raycast(ray, out hit, RayDistance, playerLayerMask);
                if (hitSomething)
                {
                    if (hit.distance < DefaultActivationDistance)
                    {
                        if (LootCheck(hit, out loot))
                        {
                            enableLootMenu = true;
                        }
                        else
                        {
                            enableLootMenu = false;
                        }
                    }
                }
            }
            // if (GameManager.Instance.IsPlayerOnHUD)
            // {
            //     if (InputManager.Instance.GetKeyDown(KeyCode.O))
            //     {
            //         enableLootMenu = true;
            //     }
            //     else if (InputManager.Instance.GetKeyDown(KeyCode.P))
            //     {
            //         enableLootMenu = false;
            //     }
            // }
        }
        private void OnGUI()
        {
            GUI.color = Color.white;
            //guiStyle.font = DaggerfallUI.TitleFont;
            GUI.depth = 0;

            Debug.Log(enableLootMenu);
            if(enableLootMenu == true)
            {
                GUI.DrawTexture(new Rect(Screen.width - (Screen.width / 5), Screen.height / 8, Screen.width / 5, Screen.height / 2), backgroundTexture);
                GUI.Label(new Rect(Screen.width - (Screen.width / 5), Screen.height / 8, Screen.width / 5, Screen.height / 8), "text label 1", guiStyle);
            }
        }

        [Invoke(StateManager.StateTypes.Game, 0)]
        public static void Init(InitParams initParams)
        {
            Debug.Log("main init");

            GameObject lootMenuGo = new GameObject("lootMenu");
            LootMenuMain lootMenu = lootMenuGo.AddComponent<LootMenuMain>();

            ModManager.Instance.GetMod(initParams.ModTitle).IsReady = true;
        }

        
        private bool LootCheck(RaycastHit hitInfo, out DaggerfallLoot loot)
        {
            loot = hitInfo.transform.GetComponent<DaggerfallLoot>();

            return loot != null;
        }
    }
}
