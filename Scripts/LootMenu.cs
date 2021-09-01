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
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
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
        DaggerfallUnityItem item;
        ItemHelper itemHelper;
        
        DaggerfallFont daggerfallFont0000;
        DaggerfallFont daggerfallFont0003;
        DaggerfallUI daggerfallUI;
        UserInterfaceManager uiManager;

        Resolution oldResolution;

        static KeyCode downKeyCode;
        static KeyCode openKeyCode;
        static KeyCode takeKeyCode;
        static KeyCode upKeyCode;

        Texture2D backgroundTexture;

        Vector2 itemFontSize;
        Vector2 titleFontSize;
        Vector2 weightFontSize;
        Vector2 shadowPosition;

        Vector2 backgroundSize;
        Vector2 backgroundUseableSize;
        Vector2 titleSize;
        Vector2 ignoredSize;
        Vector2 itemSize;

        Vector2 backgroundPos;
        Vector2 backgroundUseablePos;
        Vector2 titlePos;
        Vector2 itemPos;
        Vector2 weightPos;

        Color textColor;
        Color redColor;
        Color shadowColor;
        
        List<string> itemNameList = new List<string>();
        List<string> itemOtherList = new List<string>();

        float freeWeight;
        float shadowPosModifier;

        bool doesFit;
        bool enableLootMenu;
        static bool updatedSettings;

        static int numberOfItemsShown;
        static float horizontalScale;
        static float verticalScale;
        static float horizontalPosition;
        static float verticalPosition;

        int itemDisplayOffset;
        int playerLayerMask;
        int selectTexturePos;
        int selectedItem;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<LootMenu>();

            mod.LoadSettingsCallback = LoadSettings;

            mod.IsReady = true;
        }

        private void Start()
        {
            daggerfallFont0000 = new DaggerfallFont(DaggerfallFont.FontName.FONT0001);
            daggerfallFont0003 = new DaggerfallFont(DaggerfallFont.FontName.FONT0003);

            daggerfallUI = GameObject.Find("DaggerfallUI").GetComponent<DaggerfallUI>();
            itemHelper = new ItemHelper();
            mainCamera = GameManager.Instance.MainCamera;
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            uiManager = DaggerfallUI.Instance.UserInterfaceManager;

            enableLootMenu = false;

            backgroundTexture = DaggerfallUI.GetTextureFromResources("background");
            backgroundTexture.filterMode = FilterMode.Point;

            itemNameList = new List<string>();
            itemOtherList = new List<string>();

            oldResolution = Screen.currentResolution;

            mod.LoadSettings();
            UpdateLayout();
        }

        private void Update()
        {
            if (updatedSettings)
            {
                UpdateLayout();
                updatedSettings = false;
            }
            
            if (GameManager.Instance.IsPlayerOnHUD)
            {
                if (InputManager.Instance.GetKeyDown(upKeyCode) || Input.GetAxis("Mouse ScrollWheel") > 0f)
                    selectedItem -= 1;
                if(InputManager.Instance.GetKeyDown(downKeyCode) || Input.GetAxis("Mouse ScrollWheel") < 0f)
                    selectedItem += 1;
                
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

                                if (loot.Items.Count == 0)
                                    enableLootMenu = false;
                            }
                            if (InputManager.Instance.GetKeyDown(takeKeyCode))
                            {
                                DoTransferItemFromIndex(loot, selectedItem);
                                UpdateText(loot, out itemNameList, out itemOtherList);

                                if (loot.Items.Count == 0)
                                    enableLootMenu = false;
                            }
                            if ((loot.ContainerType != LootContainerTypes.CorpseMarker && hit.distance > PlayerActivate.TreasureActivationDistance) || loot.ContainerType == LootContainerTypes.ShopShelves || loot.ContainerType == LootContainerTypes.HouseContainers)
                            {
                                enableLootMenu = false;
                                return;
                            }
                            
                            if(!enableLootMenu)
                            {
                                UpdateText(loot, out itemNameList, out itemOtherList);
                                
                                selectedItem = 0;

                                if (loot.Items.Count == 0)
                                {
                                    enableLootMenu = false;
                                    GameObjectHelper.RemoveLootContainer(loot);
                                }
                                else
                                    enableLootMenu = true;
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
                if ( Screen.currentResolution.width != oldResolution.width || Screen.currentResolution.height != oldResolution.height)
                    UpdateLayout();
                oldResolution = Screen.currentResolution;
                
                GUI.depth = 0;
                
                int currentItem;
                int itemWidth;
                int titleWidth = (int)daggerfallFont0000.CalculateTextWidth(itemOtherList[0], titleFontSize);
                int weightWidth = (int)daggerfallFont0003.CalculateTextWidth(itemOtherList[1], weightFontSize);

                titlePos = new Vector2(backgroundUseablePos[0] + (titleSize[0] - titleWidth * titleFontSize[0]) / 2, backgroundUseablePos[1] + (titleSize[1] - daggerfallFont0003.GlyphHeight * titleFontSize[1]) / 12);
                itemPos = new Vector2();
                weightPos = new Vector2(backgroundUseablePos[0] + (titleSize[0] - weightWidth * weightFontSize[0]) / 2, backgroundUseablePos[1] + backgroundUseableSize[1] - titleSize[1] + (titleSize[1] - daggerfallFont0003.GlyphHeight * weightFontSize[1]) / 2);

                UpdatePositions();
                Debug.Log(selectedItem);
                itemOtherList[1] = getWeightText(loot.Items.GetItem(selectedItem), selectedItem);
                
                GUI.DrawTexture(new Rect(backgroundPos, backgroundSize), backgroundTexture);

                if (DaggerfallUnity.Settings.SDFFontRendering)
                    shadowPosModifier = 3f;
                else
                    shadowPosModifier = 1;

                daggerfallFont0000.DrawText(itemOtherList[0], titlePos, titleFontSize, textColor, shadowColor, shadowPosition * titleFontSize / shadowPosModifier);

                if (doesFit || (item.IsAStack() && freeWeight > item.weightInKg))
                    daggerfallFont0003.DrawText(itemOtherList[1], weightPos, weightFontSize, textColor, shadowColor, shadowPosition * weightFontSize / shadowPosModifier);
                else
                    daggerfallFont0003.DrawText(itemOtherList[1], weightPos, weightFontSize, redColor, shadowColor, shadowPosition * weightFontSize / shadowPosModifier);
                
                currentItem = itemDisplayOffset;
                for (int i = 0; i < itemNameList.Count; i++)
                {
                    if (i < numberOfItemsShown && currentItem < itemNameList.Count)
                    {
                        Vector2 newItemFontSize = itemFontSize;

                        itemWidth = (int)daggerfallFont0003.CalculateTextWidth(itemNameList[currentItem], itemFontSize);

                        // if (itemWidth * itemFontSize[0] > itemSize[0])
                        //     newItemFontSize = new Vector2(itemSize[0] / (itemWidth * 0.8f), itemSize[0] / (itemWidth * 0.8f));

                        itemPos = new Vector2(backgroundUseablePos[0] + (itemSize[0] - itemWidth * newItemFontSize[0]) / 2, backgroundUseablePos[1] + titleSize[1] + itemSize[1] * i + (itemSize[1] - daggerfallFont0003.GlyphHeight * itemFontSize[1]) / 2);

                        if (i == selectTexturePos)
                            daggerfallFont0003.DrawText(itemNameList[currentItem], new Vector2(itemPos[0], itemPos[1]), newItemFontSize, redColor, shadowColor, shadowPosition * newItemFontSize / shadowPosModifier);
                        else
                           daggerfallFont0003.DrawText(itemNameList[currentItem], new Vector2(itemPos[0], itemPos[1]), newItemFontSize, textColor, shadowColor, shadowPosition * newItemFontSize / shadowPosModifier);
                    }
                    currentItem++;
                }
            }
        }

        private void UpdateLayout()
        {
            Debug.Log("here");
            backgroundSize = new Vector2((Screen.width * 0.2f) * horizontalScale, (Screen.height * 0.8f) * verticalScale);
            backgroundUseableSize = new Vector2(backgroundSize[0] * 0.875f, backgroundSize[1] * 0.777777f);
            titleSize = new Vector2(backgroundUseableSize[0], backgroundSize[1] * 0.12857142f);
            ignoredSize = new Vector2(backgroundUseableSize[0], backgroundUseableSize[1] * 0.01428571f);
            itemSize = new Vector2(backgroundUseableSize[0], (backgroundUseableSize[1] - ignoredSize[1] * 2 - titleSize[1] * 2) / numberOfItemsShown);

            backgroundPos = new Vector2((Screen.width - backgroundSize[0]) * horizontalPosition, (Screen.height - backgroundSize[1]) * verticalPosition);
            backgroundUseablePos = new Vector2(backgroundPos[0] + (backgroundSize[0] - backgroundUseableSize[0]) / 2, backgroundPos[1] + (backgroundSize[1] - backgroundUseableSize[1]) / 2);

            titleFontSize = new Vector2(titleSize[1] / 20, titleSize[1] / 20);
            itemFontSize = new Vector2(itemSize[0] / (numberOfItemsShown * 12), itemSize[0] / (numberOfItemsShown * 12));
            weightFontSize = new Vector2(titleSize[1] / 24, titleSize[1] / 24);

            if (!DaggerfallUnity.Settings.SDFFontRendering)
            {
                titleFontSize = titleFontSize * 0.8f;
                itemFontSize = itemFontSize * 0.8f;
                weightFontSize = weightFontSize * 0.8f;
            }

            shadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos;
        
            textColor = DaggerfallUI.DaggerfallDefaultTextColor;
            redColor = Color.red;
            shadowColor = DaggerfallUI.DaggerfallDefaultShadowColor;
        }

        private static bool SetKeyFromText(string text, out KeyCode result) //"Inspired" by code from Mighty Foot from numidium (https://www.nexusmods.com/daggerfallunity/mods/162)
        {
            bool output;
            
            if (System.Enum.TryParse(text, out result))
                output = true;
            else
                output = false;
            
            return output;
        }

        static void LoadSettings(ModSettings modSettings, ModSettingsChange change)
        {
            if(!SetKeyFromText(modSettings.GetValue<string>("Controls", "TakeKeyCode"), out takeKeyCode))
                takeKeyCode = KeyCode.E;

            if(!SetKeyFromText(modSettings.GetValue<string>("Controls", "OpenKeyCode"), out openKeyCode))
                openKeyCode = KeyCode.R;

            upKeyCode = KeyCode.UpArrow;
            downKeyCode = KeyCode.DownArrow;

            numberOfItemsShown = modSettings.GetValue<int>("Layout", "NumberOfItemsShown");

            horizontalScale = (float)modSettings.GetValue<int>("Layout", "HorizontalScale") / 100;
            verticalScale = (float)modSettings.GetValue<int>("Layout", "VerticalScale") / 100;
            horizontalPosition = (float)modSettings.GetValue<int>("Layout", "HorizontalPosition") / 100;
            verticalPosition = (float)modSettings.GetValue<int>("Layout", "VerticalPosition") / 100;

            updatedSettings = true;
        }

        private void UpdatePositions()
        {
            if (selectedItem < 0)
            {
                selectedItem = 0;
                selectTexturePos = 0;
                itemDisplayOffset = 0;
            }
            else if (selectedItem >= itemNameList.Count)
            {
                selectedItem = itemNameList.Count - 1;
                selectTexturePos = numberOfItemsShown - 1;
                itemDisplayOffset = selectedItem;
            }
            else if (selectedItem < numberOfItemsShown)
            {
                selectTexturePos = selectedItem;
                itemDisplayOffset = 0;
            }
            else if (selectedItem < itemNameList.Count - numberOfItemsShown)
            {
                selectTexturePos = numberOfItemsShown - 1;
                itemDisplayOffset = selectedItem - numberOfItemsShown + 1;
            }
            else
            {
                selectTexturePos = itemNameList.Count - selectedItem - 1;
                itemDisplayOffset = selectedItem;
            }
                
            if (itemDisplayOffset > itemNameList.Count - numberOfItemsShown && itemNameList.Count > numberOfItemsShown)
            {
                itemDisplayOffset = selectedItem - numberOfItemsShown + 1;
                selectTexturePos = numberOfItemsShown - 1;
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
                string itemName;

                item = items.GetItem(i);
                itemName = itemHelper.ResolveItemLongName(item);

                if (item.IsAStack())
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item) + " " + item.stackCount.ToString());
                else
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item));
            }

            if(String.IsNullOrEmpty(loot.entityName))
                outputItemsOther.Add("Lootpile");
            else
                outputItemsOther.Add(loot.entityName);
            
            outputItemsOther.Add(getWeightText(items.GetItem(0)));
        }

        private string getWeightText(DaggerfallUnityItem item, int index = 0)
        {
            freeWeight = GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight;

            if (item.IsAStack())
            {
                if (item.weightInKg * item.stackCount <= freeWeight)
                    doesFit = true;
                else
                    doesFit = false;
            }
            else
            {
                if (item.weightInKg <= freeWeight)
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
            ItemCollection.AddPosition order;
            
            if (item.ItemGroup == ItemGroups.Transportation || item.IsSummoned)
                return;

            if (doesFit)
            {
                if (item.IsOfTemplate(ItemGroups.Currency, (int)Currency.Gold_pieces))
                {
                    GameManager.Instance.PlayerEntity.GoldPieces += item.stackCount;
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);
                    from.RemoveItem(item);
                }
                else if (item.IsOfTemplate(ItemGroups.MiscItems, (int)MiscItems.Map))
                {
                    RecordLocationFromMap(item);
                    from.RemoveItem(item);
                }
                else
                {
                    if (item.IsQuestItem)
                        order = ItemCollection.AddPosition.Front;
                    else
                        order = ItemCollection.AddPosition.DontCare;
                    
                    GameManager.Instance.PlayerEntity.Items.Transfer(item, from, order);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                }
            }
            else
            {
                if (item.IsAStack() && freeWeight > 0)
                {
                    DaggerfallUnityItem splitItem = new DaggerfallUnityItem();
                    int canCarryAmount = (int)(freeWeight / item.weightInKg);

                    if (item.IsOfTemplate(ItemGroups.Currency, (int)Currency.Gold_pieces))
                    {
                        splitItem = from.SplitStack(item, canCarryAmount); 
                        GameManager.Instance.PlayerEntity.GoldPieces += splitItem.stackCount;
                        from.RemoveItem(splitItem);

                        DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);
                    }
                    else
                    {
                        splitItem = from.SplitStack(item, canCarryAmount);
                        
                        if (splitItem.IsQuestItem)
                            order = ItemCollection.AddPosition.Front;
                        else
                            order = ItemCollection.AddPosition.DontCare;
                        
                        GameManager.Instance.PlayerEntity.Items.Transfer(splitItem, from, order);
                        from.RemoveItem(splitItem);

                        DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);
                    }
                }
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
                DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("readMapFail"));
            }
        }
    }
}
