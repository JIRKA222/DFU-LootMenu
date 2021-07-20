using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using System.Collections;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Utility.ModSupport;



namespace LootMenuMod
{
    public class LootMenuMain : MonoBehaviour
    {
        static DaggerfallMessageBox lootMenuMessageBox;
        static LootMenuUI lootMenuUIScreen;
        Camera mainCamera;
        int playerLayerMask = 0;
        const float RayDistance = 76.8f;  
        const float DefaultActivationDistance = 6.4f;
        bool enableLootMenu = false;
        bool disableLootMenu = false;
        
        void Start()
        {
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            mainCamera = GameManager.Instance.MainCamera;
        }
        
        void Update()
        {
            DaggerfallLoot loot;
            
            Debug.Log(enableLootMenu);
            Debug.Log(disableLootMenu);
            
            Ray ray = new Ray();
            
            ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            
            RaycastHit hit;
            bool hitSomething = Physics.Raycast(ray, out hit, RayDistance, playerLayerMask);
            if (hitSomething)
            {
                if (hit.distance < DefaultActivationDistance)
                {
                    lootMenuUIScreen = new LootMenuUI(DaggerfallWorkshop.Game.DaggerfallUI.UIManager);
                if (LootCheck(hit, out loot))
                {
                        DaggerfallUI.UIManager.PushWindow(lootMenuUIScreen);
                }
                else
                {
                        disableLootMenu = true;
                }
            }
                // if (enableLootMenu == true && disableLootMenu == false)
                // {
                //     DaggerfallUI.UIManager.PushWindow(lootMenuUIScreen);
                // }
                // else if (disableLootMenu == true)
                // {
                //     lootMenuUIScreen.CloseWindow();
                // }
                Debug.Log(disableLootMenu);
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
        
        public static void DisplayMessage(string message = "Hello World!")
        {
            if (lootMenuMessageBox == null)
            {
                lootMenuMessageBox = new DaggerfallMessageBox(DaggerfallWorkshop.Game.DaggerfallUI.UIManager);
                lootMenuMessageBox.AllowCancel = true;
                lootMenuMessageBox.ClickAnywhereToClose = true;
                lootMenuMessageBox.ParentPanel.BackgroundColor = Color.clear;
            }

            lootMenuMessageBox.SetText(message);
            DaggerfallUI.UIManager.PushWindow(lootMenuMessageBox);
        }

        private bool LootCheck(RaycastHit hitInfo, out DaggerfallLoot loot)
        {
            loot = hitInfo.transform.GetComponent<DaggerfallLoot>();

            return loot != null;
        }
    }
}
