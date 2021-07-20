using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Collections;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace LootMenuMod
{
    class LootMenuUI : DaggerfallBaseWindow
    {
        Mod mod = ModManager.Instance.GetMod("LootMenu");
        Panel mainPanel;
        TextLabel testTextLabel;
        Vector2 testTextLabelPosVector;
        Vector2 testTextLabelSizeVector;

        bool first = true;
        public LootMenuUI(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
            pauseWhileOpened = false;
        }
        void Update()
        {
            base.Update();
        }

        protected override void Setup()
        {
            setupUIElements();
        }

        public override void OnPush()
        {
            base.OnPush();
        }

        public override void OnPop()
        {
            base.OnPop();
        }

        private void Start()
        {
            
        }

        private void setupUIElements()
        {
            
            
            testTextLabelPosVector = new Vector2(889, 443);
            testTextLabelSizeVector = new Vector2(200, 200);
            
            mainPanel = DaggerfallUI.AddPanel(NativePanel, AutoSizeModes.None);
            mainPanel.Size = new Vector2(1920, 1080);
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            
            testTextLabel = new TextLabel();
            testTextLabel.Text = "This is a text";
            testTextLabel.Position = testTextLabelPosVector;
            testTextLabel.Size = testTextLabelSizeVector;
            testTextLabel.Font = DaggerfallUI.LargeFont;
            mainPanel.Components.Add(testTextLabel);
        }
    }
}
