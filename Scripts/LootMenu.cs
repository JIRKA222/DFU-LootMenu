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

        Texture2D backgroundTexture;
        Texture2D borderTexture;
        
        List<string> labelText = new List<string>();

        bool enableLootMenu;

        const int itemsStartOffset = 2;
        int currentItem;
        int currentLabelOffset;
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

            currentLabelOffset = 0;
            selectedItem = 0;
            wait = 0;

            upKeyCode = KeyCode.UpArrow;
            downKeyCode = KeyCode.DownArrow;
        }

        private void Update()
        {
            if (GameManager.Instance.IsPlayerOnHUD)
            {
                if (InputManager.Instance.GetKeyDown(upKeyCode) || Input.GetAxis("Mouse ScrollWheel") > 0f)
                {
                    currentLabelOffset -= 1;
                    selectedItem -= 1;
                }
                if(InputManager.Instance.GetKeyDown(downKeyCode) || Input.GetAxis("Mouse ScrollWheel") < 0f)
                {
                    currentLabelOffset += 1;
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
                                labelText = UpdateText(loot);

                                currentLabelOffset = 0;
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
                int xSize = Screen.width / 5;
                int ySize = Screen.height / 10;
                int xPos = Screen.width - (Screen.width / 5);
                int yPos = Screen.height / 10;

                GUI.color = Color.white;
                GUI.depth = 0;
                
                if (labelText.Count < 9)
                    currentLabelOffset = 0;
                else if (currentLabelOffset < 0)
                    currentLabelOffset = 0;
                else if ((labelText.Count - 8) <= currentLabelOffset)
                    currentLabelOffset = labelText.Count - itemsStartOffset - 7; 

                if (selectedItem < 0)
                    selectedItem = 0;
                else if (selectedItem > (labelText.Count - (itemsStartOffset + 1)))
                    selectedItem = labelText.Count - (itemsStartOffset + 1);

                int selectTexturePos;
                if (selectedItem < 6)
                {
                    selectTexturePos = selectedItem + 3;
                    if (currentLabelOffset > 0)
                        currentLabelOffset -= 1;
                }
                else
                    selectTexturePos = 8;

                int currentItem = currentLabelOffset + itemsStartOffset;

                GUI.DrawTexture(new Rect(new Vector2(xPos, Screen.height / 10), backgroundTextureSize), backgroundTexture);
                GUI.DrawTexture(new Rect(xPos, yPos * selectTexturePos, xSize, ySize), borderTexture);

                GUI.Label(new Rect(xPos, yPos * 1, xSize, ySize), labelText[0], guiStyle);
                GUI.Label(new Rect(xPos, yPos * 2, xSize, ySize), labelText[1] + " Kg", guiStyle);

                for (int i = itemsStartOffset; i < labelText.Count; i++)
                {
                    if (i < 9 && currentItem < labelText.Count)
                        GUI.Label(new Rect(xPos, yPos * (i + 1), xSize, ySize), labelText[currentItem], guiStyle);

                    currentItem++;
                }
            }
        }

        private bool LootCheck(RaycastHit hitInfo, out DaggerfallLoot loot)
        {
            loot = hitInfo.transform.GetComponent<DaggerfallLoot>();

            return loot != null;
        }

        private List<string> UpdateText(DaggerfallLoot loot)
        {
            List<string> output = new List<string>();
            ItemCollection items = loot.Items;

            if (items.Count == 0)
                return output;

            if(String.IsNullOrEmpty(loot.entityName))
                output.Add("Lootpile");
            else
                output.Add(loot.entityName);
            
            output.Add(items.GetWeight().ToString());

            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item;
                item = items.GetItem(i);
                output.Add(itemHelper.ResolveItemLongName(item));
            }
            return output;
        }
    }
}
