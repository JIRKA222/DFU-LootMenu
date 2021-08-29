using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using DaggerfallConnect;

using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
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
        DaggerfallLoot loot;
        DaggerfallUI daggerfallUI;
        GUIStyle guiStyle;
        ItemHelper itemHelper;
        UserInterfaceManager uiManager;

        KeyCode downKeyCode;
        KeyCode openKeyCode;
        KeyCode upKeyCode;
        KeyCode takeKeyCode;

        Texture2D backgroundTexture;
        Texture2D borderTexture;
        
        List<string> itemNameList = new List<string>();
        List<string> itemOtherList = new List<string>();

        bool enableLootMenu;

        const int numOfItemLabels = 6;
        int playerLayerMask;
        int ignoredItemAmount = 0;
        int selectedItem;
        bool doesFit;

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
            uiManager = DaggerfallUI.Instance.UserInterfaceManager;

            uiScaleModifier = 4;

            guiStyle = new GUIStyle();
            UIScale = daggerfallUI.DaggerfallHUD.HUDVitals.Scale.x * uiScaleModifier;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.fontSize = (int)(10.0f * UIScale);

            enableLootMenu = false;

            backgroundTexture = DaggerfallUI.GetTextureFromResources("background");
            backgroundTexture.filterMode = FilterMode.Point;
            borderTexture= DaggerfallUI.GetTextureFromResources("border");
            borderTexture.filterMode = FilterMode.Point;

            selectedItem = 0;

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
                    selectedItem -= 1;
                if(InputManager.Instance.GetKeyDown(downKeyCode) || Input.GetAxis("Mouse ScrollWheel") < 0f)
                    selectedItem += 1;
            }
            if (GameManager.Instance.IsPlayerOnHUD)
            {
                Ray ray = new Ray();
                ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

                RaycastHit hit;
                bool hitSomething = Physics.Raycast(ray, out hit, PlayerActivate.CorpseActivationDistance, playerLayerMask);

                if (hitSomething)
                {
                    if (hit.distance <= PlayerActivate.CorpseActivationDistance)
                    {      
                        if (LootCheck(hit, out loot))
                        {
                            if (InputManager.Instance.GetKeyDown(openKeyCode))
                            {    
                                ActivateLootContainer(hit, loot);
                                UpdateText(loot, out itemNameList, out itemOtherList);

                                if (loot.Items.Count - ignoredItemAmount == 0)
                                    enableLootMenu = false;
                            }
                            if (InputManager.Instance.GetKeyDown(takeKeyCode))
                            {
                                DoTransferItemFromIndex(loot, selectedItem);
                                UpdateText(loot, out itemNameList, out itemOtherList);

                                if (loot.Items.Count - ignoredItemAmount == 0)
                                    enableLootMenu = false;
                            }
                            if (loot.ContainerType != LootContainerTypes.CorpseMarker && hit.distance > PlayerActivate.TreasureActivationDistance)
                            {
                                enableLootMenu = false;
                                return;
                            }
                            
                            if(!enableLootMenu)
                            {
                                UpdateText(loot, out itemNameList, out itemOtherList);
                                
                                ignoredItemAmount = 0;
                                selectedItem = 0;

                                if (loot.Items.Count - ignoredItemAmount == 0)
                                {
                                    enableLootMenu = false;
                                    GameObjectHelper.RemoveLootContainer(loot);
                                }
                                else
                                {
                                    enableLootMenu = true;
                                }
                            }
                        }
                        else
                            enableLootMenu = false;
                    }
                }
                else
                    enableLootMenu = false;
            }
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

                if (selectedItem < 0)
                {
                    selectedItem = 0;
                    selectTexturePos = 0;
                    itemDisplayOffset = 0;
                }
                else if (selectedItem >= itemNameList.Count)
                {
                    selectedItem = itemNameList.Count - 1;
                    selectTexturePos = numOfItemLabels - 1;
                    itemDisplayOffset = selectedItem;
                }
                else if (selectedItem < numOfItemLabels)
                {
                    selectTexturePos = selectedItem;
                    itemDisplayOffset = 0;
                }
                else if (selectedItem < itemNameList.Count - numOfItemLabels)
                {
                    selectTexturePos = numOfItemLabels - 1;
                    itemDisplayOffset = selectedItem - numOfItemLabels + 1;
                }
                else
                {
                    selectTexturePos = itemNameList.Count - selectedItem - 1;
                    itemDisplayOffset = selectedItem;
                }
                
                if (itemDisplayOffset > itemNameList.Count - numOfItemLabels && itemNameList.Count > numOfItemLabels)
                {
                    itemDisplayOffset = selectedItem - numOfItemLabels + 1;
                    selectTexturePos = 5;
                }

                guiStyle.normal.textColor = Color.black;
                GUI.depth = 0;

                itemOtherList[1] = getWeightText(loot.Items.GetItem(selectedItem), selectedItem);

                GUI.DrawTexture(new Rect(new Vector2(xPos, Screen.height / 10), backgroundTextureSize), backgroundTexture);
                GUI.DrawTexture(new Rect(xPos, yPos * (selectTexturePos + 3), xSize, ySize), borderTexture);

                int currentItem = itemDisplayOffset;
                for (int i = 0; i < itemNameList.Count; i++)
                {
                    if (i < numOfItemLabels && currentItem < itemNameList.Count)
                        GUI.Label(new Rect(xPos, yPos * (i + 3), xSize, ySize), itemNameList[currentItem], guiStyle);
                    currentItem++;
                }

                GUI.Label(new Rect(xPos, yPos * 1, xSize, ySize), itemOtherList[0], guiStyle);

                if (!doesFit)
                    guiStyle.normal.textColor = Color.red;

                GUI.Label(new Rect(xPos, yPos * 2, xSize, ySize), itemOtherList[1], guiStyle);

                guiStyle.normal.textColor = Color.red;
                GUI.Label(new Rect((xPos / 4) * 3, yPos * 0, xSize, ySize), "E - Take", guiStyle);
                GUI.Label(new Rect(xPos, yPos * 0, xSize, ySize), "R - Open", guiStyle);
            }
        }

        private bool LootCheck(RaycastHit hitInfo, out DaggerfallLoot loot)
        {
            loot = hitInfo.transform.GetComponent<DaggerfallLoot>();

            return loot != null;
        }

        private int WeightInGPUnits(float weight)
        {
            return Mathf.RoundToInt(weight * 400f);
        }

        private void UpdateText(DaggerfallLoot loot, out List<string> outputItemsNames, out List<string> outputItemsOther)
        {
            ItemCollection items = loot.Items;
            outputItemsNames = new List<string>();
            outputItemsOther = new List<string>();

            if (items.Count == 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item;
                string itemName;

                item = items.GetItem(i);
                itemName = itemHelper.ResolveItemLongName(item);
                if (item.IsSummoned || item.ItemGroup == ItemGroups.Transportation)
                {    
                    ignoredItemAmount += 1;
                }
                else if (item.IsAStack())
                {
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item) + " " + item.stackCount.ToString());
                }
                else
                {
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item));
                }
            }

            if(String.IsNullOrEmpty(loot.entityName))
                outputItemsOther.Add("Lootpile");
            else
                outputItemsOther.Add(loot.entityName);
            
            outputItemsOther.Add(getWeightText(items.GetItem(0)));
        }

        private string getWeightText(DaggerfallUnityItem item, int index = 0)
        {
            if (item.IsAStack())
            {
                if (item.weightInKg * item.stackCount <= GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight)
                    doesFit = true;
                else
                    doesFit = false;
            }
            else
            {
                if (item.weightInKg <= GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight)
                    doesFit = true;
                else
                    doesFit = false;
            }
            string output = Math.Round(GameManager.Instance.PlayerEntity.CarriedWeight, 1).ToString() + " + " + Math.Round(item.weightInKg, 1).ToString() + " / " + GameManager.Instance.PlayerEntity.MaxEncumbrance.ToString();
            return output;
        }

        private void ActivateLootContainer(RaycastHit hit, DaggerfallLoot loot)
        {
            enableLootMenu = false;
            
            UnityEngine.Random.InitState(Time.frameCount);
            
            DaggerfallUI.Instance.InventoryWindow.LootTarget = loot;
            DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenInventoryWindow);
        }

        private void DisableEmptyCorpseContainer(GameObject go)
        {
            if (go)
            {
                SphereCollider sphereCollider = go.GetComponent<SphereCollider>();
                if (sphereCollider)
                    sphereCollider.enabled = false;
            }
        }
        
        private void DoTransferItemFromIndex(DaggerfallLoot loot, int index)
        {
            if (index < loot.Items.Count)
                DoTransferItem(loot.Items.GetItem(index), loot.Items);
        }

        private void DoTransferItem(DaggerfallUnityItem item, ItemCollection from)
        {
            if (item.ItemGroup == ItemGroups.Transportation)
                return;
            
            if (item.IsOfTemplate(ItemGroups.Currency, (int)Currency.Gold_pieces) && doesFit)
            {
                GameManager.Instance.PlayerEntity.GoldPieces += item.stackCount;
                from.RemoveItem(item);
                return;
            }

            if (item.IsOfTemplate(ItemGroups.MiscItems, (int)MiscItems.Map) && doesFit)
            {
                RecordLocationFromMap(item);
                from.RemoveItem(item);
                return;
            }

            if (!doesFit && item.IsAStack())
            {
                DaggerfallUnityItem splitItem = new DaggerfallUnityItem();
                if (item.IsOfTemplate(ItemGroups.Currency, (int)Currency.Gold_pieces))
                {
                    for (int i = item.stackCount - 1; i > 0 ; i--)
                    {
                        if (item.weightInKg * i <= GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight)
                        {
                            splitItem = from.SplitStack(item, i);
                            GameManager.Instance.PlayerEntity.GoldPieces += splitItem.stackCount;

                            from.RemoveItem(splitItem);x
                        }
                    }
                }
                else
                {
                    for (int i = item.stackCount - 1; i > 0 ; i--)
                    {
                        if (item.weightInKg * i <= GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight)
                        {
                            splitItem = from.SplitStack(item, i);
                        }
                    }

                    ItemCollection.AddPosition order = ItemCollection.AddPosition.DontCare;
                    if (splitItem.IsQuestItem)
                        order = ItemCollection.AddPosition.Front;

                    GameManager.Instance.PlayerEntity.Items.Transfer(splitItem, from, order);
                }
            }
            else
            {
                ItemCollection.AddPosition order = ItemCollection.AddPosition.DontCare;
                if (item.IsQuestItem)
                    order = ItemCollection.AddPosition.Front;

                GameManager.Instance.PlayerEntity.Items.Transfer(item, from, order);
            }

        }

        void RecordLocationFromMap(DaggerfallUnityItem item)
        {
            const int mapTextId = 499;
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;

            try
            {
                DFLocation revealedLocation = playerGPS.DiscoverRandomLocation();

                if (string.IsNullOrEmpty(revealedLocation.Name))
                    throw new Exception();

                playerGPS.LocationRevealedByMapItem = revealedLocation.Name;
                GameManager.Instance.PlayerEntity.Notebook.AddNote(
                    TextManager.Instance.GetLocalizedText("readMap").Replace("%map", revealedLocation.Name));

                DaggerfallMessageBox mapText = new DaggerfallMessageBox(uiManager);
                mapText.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRandomTokens(mapTextId));
                mapText.ParentPanel.BackgroundColor = Color.clear;
                mapText.ClickAnywhereToClose = true;
                mapText.Show();
            }
            catch (Exception)
            {
                // Player has already descovered all valid locations in this region!
                DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("readMapFail"));
            }
        }
    }
}
