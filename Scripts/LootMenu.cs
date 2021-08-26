using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;

using UnityEngine;

namespace LootMenuMod
{
    public class ContainerLootSpawnedEventArgs : System.EventArgs
    {
        public LootContainerTypes ContainerType;
        public ItemCollection Loot;
    }
    
    public class LootMenu : MonoBehaviour
    {
        private static Mod mod;
        
        Camera mainCamera;
        DaggerfallUI daggerfallUI;
        GUIStyle guiStyle = new GUIStyle();
        ItemHelper itemHelper;

        KeyCode downKeyCode;
        KeyCode upKeyCode;
        KeyCode takeKeyCode;
        KeyCode openKeyCode;

        Texture2D backgroundTexture;
        Texture2D borderTexture;
        
        List<string> itemNameList = new List<string>();
        List<string> itemOtherList = new List<string>();

        bool enableLootMenu;

        const int numOfItemLabels = 6;
        int playerLayerMask;
        int selectedItem;
        int wait;

        float UIScale;
        float uiScaleModifier;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<LootMenu>();

            mod.IsReady = true;
        }

        private void Start()
        {
            daggerfallUI = GameObject.Find("DaggerfallUI").GetComponent<DaggerfallUI>();
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            mainCamera = GameManager.Instance.MainCamera;
            itemHelper = new ItemHelper();

            uiScaleModifier = 4;

            UIScale = daggerfallUI.DaggerfallHUD.HUDVitals.Scale.x * uiScaleModifier;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.fontSize = (int)(10.0f * UIScale);

            enableLootMenu = false;

            backgroundTexture = DaggerfallUI.GetTextureFromResources("background");
            backgroundTexture.filterMode = FilterMode.Point;
            borderTexture= DaggerfallUI.GetTextureFromResources("border");
            borderTexture.filterMode = FilterMode.Point;

            selectedItem = 0;
            wait = 0;

            upKeyCode = KeyCode.UpArrow;
            downKeyCode = KeyCode.DownArrow;
            takeKeyCode = KeyCode.E;
            openKeyCode = KeyCode.R;

            itemNameList = new List<string>();
            itemOtherList = new List<string>();
        }

        private void Update()
        {
            if (GameManager.Instance.IsPlayerOnHUD)
            {
                if (InputManager.Instance.GetKeyDown(upKeyCode) || Input.GetAxis("Mouse ScrollWheel") > 0f)
                {
                    selectedItem -= 1;
                }
                if(InputManager.Instance.GetKeyDown(downKeyCode) || Input.GetAxis("Mouse ScrollWheel") < 0f)
                {
                    selectedItem += 1;
                }
 
            }
            if (GameManager.Instance.IsPlayerOnHUD && wait > 2)
            {
                wait = 0;
                
                Ray ray = new Ray();
                ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

                RaycastHit hit;
                bool hitSomething = Physics.Raycast(ray, out hit, PlayerActivate.CorpseActivationDistance, playerLayerMask);

                if (hitSomething)
                {
                    DaggerfallLoot loot;
                    if (hit.distance <= PlayerActivate.CorpseActivationDistance)
                    {      
                        if (LootCheck(hit, out loot))
                        {
                            if (loot.ContainerType != LootContainerTypes.CorpseMarker && hit.distance > PlayerActivate.TreasureActivationDistance)
                            {
                                enableLootMenu = false;
                                return;
                            }
                            
                            if(!enableLootMenu)
                            {
                                UpdateText(loot, out itemNameList, out itemOtherList);

                                selectedItem = 0;
                                enableLootMenu = true;
                            }
                        }
                        else
                        {
                            enableLootMenu = false;
                        }
                    }
                }
                else
                {
                    enableLootMenu = false;
                }
            }
            wait += 1;
        }

        private void OnGUI()
        {
            if (enableLootMenu && GameManager.Instance.IsPlayerOnHUD)
            {   
                Vector2 backgroundTextureSize = new Vector2(Screen.width / 5, Screen.height * 0.8f);

                int selectTexturePos = 0;
                int itemDisplayOffset = 0;
                int xPos = Screen.width - (Screen.width / 5);
                int xSize = Screen.width / 5;
                int yPos = Screen.height / 10;
                int ySize = Screen.height / 10;

                GUI.color = Color.white;
                GUI.depth = 0;
                Debug.Log("");
                if (selectedItem < 0)
                {
                    Debug.Log("1");
                    selectedItem = 0;
                    selectTexturePos = 0;
                    itemDisplayOffset = 0;
                }
                else if (selectedItem < numOfItemLabels && selectedItem >= itemNameList.Count)
                {
                    Debug.Log("2");
                    selectedItem = itemNameList.Count - 1;
                    selectTexturePos = itemNameList.Count - 1;
                    itemDisplayOffset = 0;
                }
                else if (selectedItem < numOfItemLabels)
                {
                    Debug.Log("3");
                    selectTexturePos = selectedItem;
                    itemDisplayOffset = 0;
                }
                else if (selectedItem >= itemNameList.Count)
                {
                    Debug.Log("4");
                    selectedItem = itemNameList.Count - 1;
                    selectTexturePos = 5;
                    itemDisplayOffset = selectedItem - itemNameList.Count;
                }
                else
                {
                    Debug.Log("5");
                    selectTexturePos = numOfItemLabels - 1;
                    itemDisplayOffset = selectedItem;
                }

                if (itemDisplayOffset > itemNameList.Count - numOfItemLabels && itemNameList.Count > numOfItemLabels)
                {    
                    itemDisplayOffset = itemNameList.Count - numOfItemLabels;
                    Debug.Log("5 - 0");
                }
                
                Debug.Log(selectedItem);
                Debug.Log(selectTexturePos);
                Debug.Log(itemDisplayOffset);

                GUI.DrawTexture(new Rect(new Vector2(xPos, Screen.height / 10), backgroundTextureSize), backgroundTexture);
                GUI.DrawTexture(new Rect(xPos, yPos * (selectTexturePos + 3), xSize, ySize), borderTexture);

                GUI.Label(new Rect(xPos, yPos * 1, xSize, ySize), itemOtherList[0], guiStyle);
                GUI.Label(new Rect(xPos, yPos * 2, xSize, ySize), itemOtherList[1] + " Kg", guiStyle);

                int currentItem = itemDisplayOffset;
                for (int i = 0; i < itemNameList.Count; i++)
                {
                    if (i < numOfItemLabels && currentItem < itemNameList.Count)
                    {
                        Debug.Log(itemNameList[currentItem]);
                        
                        GUI.Label(new Rect(xPos, yPos * (i + 3), xSize, ySize), itemNameList[currentItem], guiStyle);
                    }
                    currentItem++;
                }
            }
        }

        private bool LootCheck(RaycastHit hitInfo, out DaggerfallLoot loot)
        {
            loot = hitInfo.transform.GetComponent<DaggerfallLoot>();

            return loot != null;
        }

        private void UpdateText(DaggerfallLoot loot, out List<string> outputItemsNames, out List<string> outputItemsOther)
        {
            ItemCollection items = loot.Items;
            outputItemsNames = new List<string>();
            outputItemsOther = new List<string>();

            if (items.Count == 0)
                return;

            if(String.IsNullOrEmpty(loot.entityName))
                outputItemsOther.Add("Lootpile");
            else
                outputItemsOther.Add(loot.entityName);
            
            outputItemsOther.Add(items.GetWeight().ToString());

            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item;
                string itemName;

                item = items.GetItem(i);
                itemName = itemHelper.ResolveItemLongName(item);
                if (itemName != "Gold Pieces")
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item));
            }
        }
    }
}
