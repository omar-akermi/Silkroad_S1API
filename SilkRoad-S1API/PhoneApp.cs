// This version rewrites all UI building logic to use UIFactory abstraction.
using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using S1API.UI;
using SilkRoad;
using ProductManager = S1API.Products.ProductManager;
using S1API.Internal.Utils;
using S1API.Utils;
using ProductDefinition = S1API.Products.ProductDefinition;
using S1API.Products;

namespace SilkRoad
{
    public class MyApp : S1API.PhoneApp.PhoneApp
    {
        protected override string AppName => "Silkroad";
        protected override string AppTitle => "Silkroad";
        protected override string IconLabel => "Silkroad";
        protected override string IconFileName => "silkroad/SilkroadIcon.png";

        private List<QuestData> quests;
        private RectTransform questListContainer;
        private Text questTitle, questTask, questReward, deliveryStatus, acceptLabel;
        private Button acceptButton;
        
        protected override void OnCreatedUI(GameObject container)
        {
            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);

            var topBar = UIFactory.Panel("TopBar", bg.transform, new Color(0.15f, 0.15f, 0.15f), new Vector2(0, 0.93f), new Vector2(1, 1));
            UIFactory.Text("AppTitle", "Silk Road", topBar.transform, 26, TextAnchor.MiddleCenter, FontStyle.Bold);

            var leftPanel = UIFactory.Panel("QuestListPanel", bg.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0.02f, 0f), new Vector2(0.49f, 0.93f));
            var separator = UIFactory.Panel("Separator", bg.transform, new Color(0.2f, 0.2f, 0.2f),
                new Vector2(0.485f, 0f), new Vector2(0.487f, 0.93f));
            questListContainer = UIFactory.ScrollableVerticalList("QuestListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);

            var rightPanel = UIFactory.Panel("DetailPanel", bg.transform, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0.49f, 0f), new Vector2(0.98f, 0.93f));

            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 12, padding: new RectOffset(10, 40, 10, 65));

            questTitle = UIFactory.Text("Title", "Select a quest", rightPanel.transform, 22, TextAnchor.UpperLeft, FontStyle.Bold);
            questTask = UIFactory.Text("Task", "Task: --", rightPanel.transform, 18);
            questReward = UIFactory.Text("Reward", "Reward: --", rightPanel.transform, 18);
            deliveryStatus = UIFactory.Text("Delivery", "", rightPanel.transform, 16);


            if (rightPanel == null || rightPanel.transform == null)
            {
                MelonLogger.Error("❌ rightPanel or its transform is null before creating Accept Button.");
                return;
            }
            var (acceptGO, acceptBtn, acceptLbl) = UIFactory.ButtonWithLabel("AcceptBtn", "Accept Delivery", rightPanel.transform, new Color(0.2f, 0.6f, 0.2f));
            acceptButton = acceptBtn;
            acceptLabel = acceptLbl;


            LoadQuests();
        }

        private void LoadQuests()
        {
            quests = new List<QuestData>();
            var discovered = ProductManager.DiscoveredProducts?.ToList();
            if (discovered == null || discovered.Count == 0)
            {
                MelonLogger.Warning("No products found for quests.");
                return;
            }

            var used = new HashSet<string>();
            for (int i = 0; i < Math.Min(8, discovered.Count); i++)
            {
                var def = discovered.PickUnique(p => used.Contains(p.Name), 10);
                if (def == null) continue;

                used.Add(def.Name);
                int amount = RandomUtils.RangeInt(20, 100);
                int bonus = RandomUtils.RangeInt(10, 50);
                int reward = Mathf.RoundToInt(def.Price * 20f * amount + bonus * amount);

                quests.Add(new QuestData
                {
                    Title = $"{def.Name} Delivery",
                    Task = $"Deliver {amount}x {def.Name} bricks.",
                    Reward = reward,
                    ProductID = def.Name,
                    AmountRequired = (uint)amount
                });
            }

            RefreshQuestList();
        }


        private void RefreshQuestList()
        {
            MelonLogger.Msg("RefreshQuestList called. quests.Count=" + (quests != null ? quests.Count.ToString() : "null"));
            UIFactory.ClearChildren(questListContainer);

            foreach (var quest in quests)
            {
                if (quest == null) { MelonLogger.Warning("Null quest encountered in RefreshQuestList."); continue; }
                
                
                var def = ProductManager.DiscoveredProducts.FirstOrDefault(p => p.Name == quest.ProductID);

                if (def == null)
                {
                    MelonLogger.Warning("Product not found: " + quest.ProductID);
                    continue;
                }

                MelonLogger.Msg($"Type: {def.GetType().FullName}");

                string mafiaLabel = "Client: Unknown";
                string mafiaIcon = "silkroad/mafia_unknown.png";

                if (def is WeedDefinition)
                {
                    mafiaLabel = "Client: German Mafia";
                    mafiaIcon = "silkroad/mafia_german.png";
                }
                else if (def is MethDefinition)
                {
                    mafiaLabel = "Client: Canadian Mafia";
                    mafiaIcon = "silkroad/mafia_canadian.png";
                }
                else if (def is CocaineDefinition)
                {
                    mafiaLabel = "Client: Russian Mafia";
                    mafiaIcon = "silkroad/mafia_russian.png";
                }



                Sprite iconSprite = ImageUtils.LoadImage(mafiaIcon);
                if (iconSprite == null)
                {
                    MelonLogger.Warning("Failed to load mafia icon sprite: " + mafiaIcon);
                    continue;
                }

                var row = UIFactory.CreateQuestRow(quest.Title, questListContainer, out var iconPanel, out var textPanel);
                UIFactory.SetIcon(iconSprite, iconPanel.transform);
                ButtonUtils.AddListener(row.GetComponent<Button>(), () => OnSelectQuest(quest));

                UIFactory.CreateTextBlock(textPanel.transform, quest.Title, mafiaLabel,
                    QuestDelivery.CompletedQuestKeys?.Contains($"{quest.ProductID}_{quest.AmountRequired}") == true);

                bool canSelect = !QuestDelivery.QuestActive &&
                         !(QuestDelivery.CompletedQuestKeys?.Contains($"{quest.ProductID}_{quest.AmountRequired}") ?? false);
        MelonLogger.Msg($"Quest {quest.Title}: canSelect={canSelect}, active={QuestDelivery.QuestActive}");

                ButtonUtils.AddListener(row.AddComponent<Button>(), () => OnSelectQuest(quest));


            }
        }

        private void OnSelectQuest(QuestData quest)
        {
            questTitle.text = quest.Title;
            questTask.text = $"Task: {quest.Task}";
            questReward.text = $"Reward: ${quest.Reward:N0}";
            deliveryStatus.text = "";

            ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
            ButtonUtils.ClearListeners(acceptButton);
            ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
        }

        private void AcceptQuest(QuestData quest)
        {
            string questKey = $"{quest.ProductID}_{quest.AmountRequired}";

            if (QuestDelivery.QuestActive)
            {
                ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color(0.2f, 0.4f, 0.8f));
                deliveryStatus.text = "⚠️ Finish your current job first!";
                return;
            }

            if (QuestDelivery.CompletedQuestKeys.Contains(questKey))
            {
                ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color(0.2f, 0.6f, 0.2f));
                deliveryStatus.text = "⛔ Already delivered this shipment.";
                ButtonUtils.Disable(acceptButton, acceptLabel, "Already Delivered");
                return;
            }
            deliveryStatus.text = "📦 Delivery started!";
            ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");

            var q = S1API.Quests.QuestManager.CreateQuest<QuestDelivery>();
            if (q is QuestDelivery delivery)
            {
                delivery.Data.ProductID = quest.ProductID;
                delivery.Data.RequiredAmount = quest.AmountRequired;
                delivery.Data.Reward = quest.Reward;
            }

            ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color(0.2f, 0.4f, 0.8f));


            deliveryStatus.text = "📦 Delivery started!";
            acceptButton.interactable = false;
        }

        public void RefreshAcceptButton()
        {
            ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color(0.2f, 0.6f, 0.2f));

        }
    }
}