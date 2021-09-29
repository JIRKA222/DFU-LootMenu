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
    public class LootMenu : MonoBehaviour
    {
        private static Mod mod;
        
        Camera mainCamera;

        DaggerfallLoot loot;
        DaggerfallUnityItem item;
        ItemHelper itemHelper;
        
        DaggerfallFont daggerfallFont0001;
        DaggerfallFont daggerfallFont0003;
        DaggerfallUI daggerfallUI;
        UserInterfaceManager uiManager;

        Resolution oldResolution;

        static KeyCode downKeyCode;
        static KeyCode openKeyCode;
        static KeyCode takeKeyCode;
        static KeyCode upKeyCode;

        List<Texture2D> itemTextures;
        Texture2D backgroundTexture;
        Texture2D weightTexture;

        Vector2 itemFontSize;
        Vector2 titleFontSize;
        Vector2 weightFontSize;
        Vector2 shadowPosition;

        Vector2 backgroundSize;
        Vector2 backgroundUseableSize;
        Vector2 titleSize;
        Vector2 ignoredSize;
        Vector2 itemSize;
        Vector2 itemTextureSize;

        Vector2 backgroundPos;
        Vector2 backgroundUseablePos;

        Color textColor;
        Color redColor;
        Color shadowColor;
        Color magicColor;
        Color redMagicColor;
        
        List<Byte> itemSpecialPropersties;
        List<string> itemNameList;
        List<string> itemOtherList;

        float freeWeight;
        float shadowPosModifier;

        bool doesFit;
        bool enableLootMenu;

        static bool updatedSettings;
        static bool showWeightIcon;

        static int currentLayout; // 0 - Classic, 1 - Icons
        static int numberOfItemsShown;

        static float horizontalScale;
        static float verticalScale;
        static float titleFontSizeScale;
        static float itemFontSizeScale;
        static float weightFontSizeScale;

        static int decimalSeparator;
        static int titlePosYOffset;
        static int itemPosYOffset;
        static int weightPosYOffset;

        static float horizontalPositionModifier;
        static float verticalPositionModifier;

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
            mainCamera = GameManager.Instance.MainCamera;
            
            itemHelper = new ItemHelper();
            
            daggerfallFont0001 = new DaggerfallFont(DaggerfallFont.FontName.FONT0001);
            daggerfallFont0003 = new DaggerfallFont(DaggerfallFont.FontName.FONT0003);
            daggerfallUI = GameObject.Find("DaggerfallUI").GetComponent<DaggerfallUI>();
            uiManager = DaggerfallUI.Instance.UserInterfaceManager;
            
            oldResolution = Screen.currentResolution;
            
            enableLootMenu = false;

            if(!TextureReplacement.TryImportTextureFromLooseFiles("LootMenu/lootMenuBackground", false, false, true, out backgroundTexture))
                throw new Exception("LootMenu: Could not load lootMenuBackground texture.");
            backgroundTexture.filterMode = FilterMode.Point;

            weightTexture = itemHelper.GetContainerImage(InventoryContainerImages.Backpack).texture;
            weightTexture.filterMode = FilterMode.Point;

            if (DaggerfallUnity.Settings.SDFFontRendering)
                shadowPosModifier = 3f;
            else
                shadowPosModifier = 1;

            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));

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
                            if ((loot.ContainerType != LootContainerTypes.CorpseMarker && hit.distance > PlayerActivate.TreasureActivationDistance) || loot.ContainerType == LootContainerTypes.ShopShelves || loot.ContainerType == LootContainerTypes.HouseContainers)
                            {
                                enableLootMenu = false;
                                return;
                            }
                            
                            if (InputManager.Instance.GetKeyDown(openKeyCode))
                                ActivateLootContainer(hit, loot);
                            
                            if (InputManager.Instance.GetKeyDown(takeKeyCode))
                                DoTransferItemFromIndex(loot, selectedItem);
                            
                            if(!enableLootMenu)
                            {
                                selectedItem = 0;

                                if (loot.Items.Count == 0)
                                {
                                    enableLootMenu = false;
                                    GameObjectHelper.RemoveLootContainer(loot);
                                }
                                else
                                    enableLootMenu = true;
                            }

                            if (enableLootMenu)
                            {
                                List<string> oldItemNameList = itemNameList;
                                UpdateText(loot, out itemNameList, out itemOtherList);

                                if (itemNameList != oldItemNameList)
                                    UpdateItemTextures(loot, out itemTextures);
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
                if (ResolutionChange())
                    UpdateLayout();
                
                UpdatePositions();
                
                GUI.depth = 0;

                int currentItem;
                
                int titleWidth = (int)daggerfallFont0001.CalculateTextWidth(itemOtherList[0], titleFontSize);
                int weightWidth = (int)daggerfallFont0003.CalculateTextWidth(itemOtherList[1], weightFontSize);
                int itemWidth;

                Vector2 newTitleFontSize = GetTitleMaxFontSize(titleWidth);
                Vector2 newWeightFontSize = GetWeightMaxFontSize(weightWidth);
                Vector2 newItemFontSize;
                
                Vector2 titlePos = GetTitleTextPos(titleWidth, newTitleFontSize);
                Vector2 weightPos = GetWeightTextPos(weightWidth, newWeightFontSize);
                Vector2 itemPos;

                itemOtherList[1] = getWeightText(loot.Items.GetItem(selectedItem), selectedItem);
                
                GUI.DrawTexture(new Rect(backgroundPos, backgroundSize), backgroundTexture);

                daggerfallFont0001.DrawText(itemOtherList[0], titlePos, newTitleFontSize, textColor, shadowColor, shadowPosition * newTitleFontSize / shadowPosModifier);

                if (doesFit || (item.IsAStack() && freeWeight > item.weightInKg))
                    daggerfallFont0003.DrawText(itemOtherList[1], weightPos, weightFontSize, textColor, shadowColor, shadowPosition * weightFontSize / shadowPosModifier);
                else
                    daggerfallFont0003.DrawText(itemOtherList[1], weightPos, weightFontSize, redColor, shadowColor, shadowPosition * weightFontSize / shadowPosModifier);

                if(showWeightIcon)
                {
                    if (DaggerfallUnity.Settings.SDFFontRendering)
                        GUI.DrawTexture(new Rect(new Vector2(weightPos[0] + (weightWidth * weightFontSize[0]) + (titleSize[1] / 4), weightPos[1] - (titleSize[1] / 12)), new Vector2(titleSize[1] * 0.5f, titleSize[1] * 0.5f)), weightTexture);
                    else
                        GUI.DrawTexture(new Rect(new Vector2(weightPos[0] + (weightWidth * weightFontSize[0]) + (titleSize[1] / 24), weightPos[1] - (titleSize[1] / 12)), new Vector2(titleSize[1] * 0.5f, titleSize[1] * 0.5f)), weightTexture);
                }

                currentItem = itemDisplayOffset;
                Debug.Log("");
                for (int i = 0; i < itemNameList.Count; i++)
                {
                    if (i < numberOfItemsShown && currentItem < itemNameList.Count)
                    {
                        Color selectedTextColor;

                        if (itemSpecialPropersties[currentItem] == 1)
                        {
                            selectedTextColor = magicColor;
                            if (i == selectTexturePos)
                                selectedTextColor = redMagicColor;
                        }
                        else
                        {
                            selectedTextColor = textColor;
                            if (i == selectTexturePos)
                                selectedTextColor = redColor;
                        }
                        
                        itemWidth = (int)daggerfallFont0003.CalculateTextWidth(itemNameList[currentItem], itemFontSize);
                        
                        if (currentLayout == 0)
                        {
                            newItemFontSize = GetItemMaxFontSize(itemWidth);
                            itemPos = GetItemTextPoS(itemWidth, i, newItemFontSize);

                            daggerfallFont0003.DrawText(itemNameList[currentItem], itemPos, newItemFontSize, selectedTextColor, shadowColor, shadowPosition * newItemFontSize / shadowPosModifier);
                        }

                        else if (currentLayout == 1)
                        {
                            newItemFontSize = GetItemMaxFontSize(itemWidth, true);
                            itemPos = GetItemTextPoS(itemWidth, i, newItemFontSize);

                            daggerfallFont0003.DrawText(itemNameList[currentItem], new Vector2(backgroundPos[0] + itemTextureSize[0] * (numberOfItemsShown / 2.5f), itemPos[1]), newItemFontSize, selectedTextColor, shadowColor, shadowPosition * newItemFontSize / shadowPosModifier);

                            Rect position = GetITemTextureRect(itemTextures[currentItem], itemTextureSize, 
                            new Vector2(backgroundPos[0] + itemTextureSize[0] * (numberOfItemsShown / 2.5f) - itemTextureSize[0] * 2.5f, itemPos[1] + itemSize[1]));

                            GUI.DrawTexture(position, itemTextures[currentItem]);   
                        }
                    }
                    currentItem++;
                }
            }
        }

        private Rect GetITemTextureRect(Texture2D inputTexture, Vector2 maxSize, Vector2 defaultPosition)
        {   
            Vector2 newPos = new Vector2();
            Vector2 newSize = new Vector2();

            float temp = 0;
            
            if (inputTexture.height > inputTexture.width)
                {
                    temp = maxSize[0] / inputTexture.height;
                    newSize[0] = (int)Math.Round(temp * inputTexture.width);
                    newSize[1] = (int)Math.Round(temp * inputTexture.height);
                    if(newSize[1] > maxSize[1])
                    {
                        float temp2 = (float)maxSize[1] / newSize[1];
                        newSize[0] = (int)Math.Round(temp2 * newSize[0]);
                        newSize[1] = maxSize[1];
                    }
                }
                else
                {
                    temp = maxSize[1] / inputTexture.width;
                    newSize[0] = (int)Math.Round(temp * inputTexture.width);
                    newSize[1] = (int)Math.Round(temp * inputTexture.height);
                    if(newSize[0] > maxSize[0])
                    {
                        float temp2 = (float)maxSize[0] / newSize[0];
                        newSize[1] = (int)Math.Round(temp2 * newSize[1]);
                        newSize[0] = maxSize[0];
                    }
                }
                newPos[0] = defaultPosition[0] + maxSize[0] + (maxSize[0] - newSize[0]) / 2;
                newPos[1] = defaultPosition[1] - maxSize[1] + (maxSize[1] - newSize[1]) / 4 - newSize[1] / 3;
                
                return new Rect(newPos, newSize);   
        }

        Vector2 GetTitleMaxFontSize(int titleWidth)
        {
            Vector2 output = titleFontSize;

            if (titleWidth * titleFontSize[0] > titleSize[0])
                output = new Vector2(titleSize[0] / titleWidth, titleSize[0] / titleWidth);

            return output;
        }

        Vector2 GetTitleTextPos(int titleWidth, Vector2 fontSize)
        {
            Vector2 output = new Vector2(backgroundUseablePos[0] + (titleSize[0] - titleWidth * fontSize[0]) / 2, backgroundUseablePos[1] + (titleSize[1] - (daggerfallFont0001.GlyphHeight * fontSize[1]) * 1.5f) / 2 + titlePosYOffset);

            return output;
        }

        Vector2 GetItemMaxFontSize(int itemWidth, bool icons = false)
        {
            Vector2 output = itemFontSize;
            if (icons)
            {
                if (itemWidth * itemFontSize[0] > itemSize[0] - itemTextureSize[0] * 2)
                    output = new Vector2((itemSize[0] - itemTextureSize[0] * 2) / itemWidth, (itemSize[0] - itemTextureSize[0] * 2) / itemWidth);
            }
            else
            {
                if (itemWidth * itemFontSize[0] > itemSize[0])
                    output = new Vector2(itemSize[0] / itemWidth, itemSize[0] / itemWidth);
            }

            return output;
        }

        Vector2 GetItemTextPoS(int itemWidth, int index, Vector2 fontSize)
        {
            Vector2 output = new Vector2(backgroundUseablePos[0] + (itemSize[0] - itemWidth * fontSize[0]) / 2, backgroundUseablePos[1] + titleSize[1] + itemSize[1] * index + (itemSize[1] - daggerfallFont0003.GlyphHeight * fontSize[1]) / 2 + itemPosYOffset);
            return output;
        }

        Vector2 GetWeightMaxFontSize(int weightWidth)
        {
            Vector2 output = weightFontSize;

            if (weightWidth * weightFontSize[0] > titleSize[0])
                output = new Vector2(titleSize[0] / weightWidth, titleSize[0] / weightWidth);

            return output;
        }

        Vector2 GetWeightTextPos(int weightWidth, Vector2 fontSize)
        {
            Vector2 output = new Vector2(backgroundUseablePos[0] + (titleSize[0] - weightWidth * fontSize[0]) / 2, backgroundUseablePos[1] + backgroundUseableSize[1] - (daggerfallFont0003.GlyphHeight * fontSize[1]) + weightPosYOffset);

            return output;
        }


        private void UpdateLayout()
        {
            backgroundSize = new Vector2((Screen.width / 6) * horizontalScale, (Screen.height * 0.5f) * verticalScale);
            backgroundUseableSize = new Vector2(backgroundSize[0] * 0.875f, backgroundSize[1] * 0.777777f);
            titleSize = new Vector2(backgroundUseableSize[0], backgroundSize[1] * 0.12857142f);
            ignoredSize = new Vector2(backgroundUseableSize[0], backgroundUseableSize[1] * 0.01428571f);
            itemSize = new Vector2(backgroundUseableSize[0], (backgroundUseableSize[1] - ignoredSize[1] * 2 - titleSize[1] * 2) / numberOfItemsShown);

            backgroundPos = new Vector2((Screen.width - backgroundSize[0]) * horizontalPositionModifier, (Screen.height - backgroundSize[1]) * verticalPositionModifier);
            backgroundUseablePos = new Vector2(backgroundPos[0] + (backgroundSize[0] - backgroundUseableSize[0]) / 2, backgroundPos[1] + (backgroundSize[1] - backgroundUseableSize[1]) / 2);

            titleFontSize = new Vector2((titleSize[1] / 18) * titleFontSizeScale, (titleSize[1] / 18) * titleFontSizeScale);
            itemFontSize = new Vector2((itemSize[0] / (numberOfItemsShown * 12) * itemFontSizeScale), (itemSize[0] / (numberOfItemsShown * 12)) * itemFontSizeScale);
            weightFontSize = new Vector2((titleSize[1] / 16) * weightFontSizeScale, (titleSize[1] / 16) * weightFontSizeScale);

            if (!DaggerfallUnity.Settings.SDFFontRendering)
            {
                titleFontSize = titleFontSize * 0.9f;
                itemFontSize = itemFontSize * 0.8f;
                weightFontSize = weightFontSize * 0.8f;
            }

            if (currentLayout == 1)
                itemTextureSize = new Vector2(itemSize[1], itemSize[1] * 0.9f);
            
            shadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos;
        
            textColor = DaggerfallUI.DaggerfallDefaultTextColor;
            redColor = Color.red;
            shadowColor = DaggerfallUI.DaggerfallDefaultShadowColor;
            magicColor = Color.cyan;
            redMagicColor = Color.blue;
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
            currentLayout = modSettings.GetValue<int>("Layout", "Layout");
            numberOfItemsShown = modSettings.GetValue<int>("Layout", "NumberOfItemsShown");
            decimalSeparator = modSettings.GetValue<int>("Text", "DecimalSeparator");
            horizontalScale = (float)modSettings.GetValue<int>("Layout", "HorizontalScale") / 100;
            verticalScale = (float)modSettings.GetValue<int>("Layout", "VerticalScale") / 100;
            horizontalPositionModifier = (float)modSettings.GetValue<int>("Layout", "HorizontalPosition") / 100;
            verticalPositionModifier = (float)modSettings.GetValue<int>("Layout", "VerticalPosition") / 100;

            titleFontSizeScale = (float)modSettings.GetValue<int>("Text", "TitleFontSize") / 100;
            itemFontSizeScale = (float)modSettings.GetValue<int>("Text", "ItemFontSize") / 100;
            weightFontSizeScale = (float)modSettings.GetValue<int>("Text", "WeightFontSize") / 100;

            titlePosYOffset = modSettings.GetValue<int>("Text", "TitleTextVerticalOffset");
            itemPosYOffset = modSettings.GetValue<int>("Text", "ItemTextVerticalOffset");
            weightPosYOffset = modSettings.GetValue<int>("Text", "WeightTextVerticalOffset");

            showWeightIcon = modSettings.GetValue<bool>("Layout", "ShowCarriedWeightIcon");

            if(!SetKeyFromText(modSettings.GetValue<string>("Controls", "TakeKeyCode"), out takeKeyCode))
                takeKeyCode = KeyCode.E;

            if(!SetKeyFromText(modSettings.GetValue<string>("Controls", "OpenKeyCode"), out openKeyCode))
                openKeyCode = KeyCode.R;

            upKeyCode = KeyCode.UpArrow;
            downKeyCode = KeyCode.DownArrow;

            updatedSettings = true;
        }

        private bool ResolutionChange()
        {
            bool output = false;

            if ( Screen.currentResolution.width != oldResolution.width || Screen.currentResolution.height != oldResolution.height)
                output = true;
            
            oldResolution = Screen.currentResolution;
            return output;
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
            itemSpecialPropersties = new List<Byte>();

            if (items.Count == 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                item = items.GetItem(i);

                if (item.IsAStack())
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item) + " " + item.stackCount.ToString());
                else
                    outputItemsNames.Add(itemHelper.ResolveItemLongName(item));
                
                if (item.IsEnchanted)
                    itemSpecialPropersties.Add(1);
                else
                    itemSpecialPropersties.Add(0);
            }
            if(String.IsNullOrEmpty(loot.entityName))
                outputItemsOther.Add("Lootpile");
            else
                outputItemsOther.Add(loot.entityName);
            
            if (items.GetItem(0) != null)
                outputItemsOther.Add(getWeightText(items.GetItem(0)));
        }

        private void UpdateItemTextures(DaggerfallLoot loot, out List<Texture2D> outputItemTextures)
        {
            ItemCollection items = loot.Items;
            outputItemTextures = new List<Texture2D>();

            if (items.Count == 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                itemTextures.Add(itemHelper.GetInventoryImage(items.GetItem(i)).texture);
                itemTextures[i].filterMode = FilterMode.Point;
            }
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
            if(decimalSeparator == 1)
                output = output.Replace(',', '.');

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
