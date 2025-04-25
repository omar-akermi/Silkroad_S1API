// This version rewrites all UI building logic to use UIFactory abstraction.
using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using S1API.GameTime;
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
        protected override string IconFileName => "silkroad\\SilkroadIcon.png";

        private List<QuestData> quests;
        private RectTransform questListContainer;
        private Text questTitle, questTask, questReward, deliveryStatus, acceptLabel,cancelLabel,refreshLabel;
        private Button acceptButton,cancelButton,refreshButton;
        

protected override void OnCreated()
{
    base.OnCreated();
    MelonLogger.Msg("[SilkRoadApp] OnCreated called");
}

        protected override void OnCreatedUI(GameObject container)
        {

            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);



            UIFactory.TopBar(
                name: "TopBar",
                parent: bg.transform,
                title: "Silk Road",
                topbarSize: 0.82f,
                paddingLeft: 75,
                paddingRight: 75,
                paddingTop: 0,
                paddingBottom: 35
            );
            
            var leftPanel = UIFactory.Panel("QuestListPanel", bg.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.82f)); 
            var separator = UIFactory.Panel("Separator", bg.transform, new Color(0.2f, 0.2f, 0.2f),
                new Vector2(0.485f, 0f), new Vector2(0.487f, 0.82f));
            questListContainer = UIFactory.ScrollableVerticalList("QuestListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);

            var rightPanel = UIFactory.Panel("DetailPanel", bg.transform, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0.49f, 0f), new Vector2(0.98f, 0.82f));

// Use vertical layout with padding and spacing like Tax & Wash
            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 14, padding: new RectOffset(24, 50, 15, 70));

// Header
            questTitle = UIFactory.Text("Title", "Select a quest", rightPanel.transform, 24, TextAnchor.MiddleLeft, FontStyle.Bold);

// Styled task/reward rows (Label + Value style)
            questTask = UIFactory.Text("Task", "Task: --", rightPanel.transform, 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            questReward = UIFactory.Text("Reward", "Reward: --", rightPanel.transform, 18, TextAnchor.MiddleLeft, FontStyle.Normal);

// Optional delivery message
            deliveryStatus = UIFactory.Text("DeliveryStatus", "", rightPanel.transform, 16, TextAnchor.MiddleLeft, FontStyle.Italic);
            deliveryStatus.color = new Color(0.7f, 0.9f, 0.7f);
            
// Create a horizontal container for Refresh and Cancel
            var topButtonRow = UIFactory.Panel("TopButtonRow", rightPanel.transform, Color.clear);
            UIFactory.HorizontalLayoutOnGO(topButtonRow, spacing: 12);
            UIFactory.SetLayoutGroupPadding(topButtonRow.GetComponent<HorizontalLayoutGroup>(), 0, 0, 0, 0);

// Create horizontal row for top buttons
            var buttonRow = UIFactory.ButtonRow("TopButtons", rightPanel.transform, spacing: 14);

// Refresh Button
            var (refreshGO, refreshBtn, refreshLbl) = UIFactory.RoundedButtonWithLabel("RefreshBtn", "Refresh Order list", buttonRow.transform, new Color32(32,0x82,0xF6,0xff), 300, 90,18,Color.white);
            refreshButton = refreshBtn;
            refreshLabel = refreshLbl;
            ButtonUtils.AddListener(refreshButton, () => RefreshButton());

// Cancel Button

            var (cancelGO, cancelBtn, cancelLbl) = UIFactory.RoundedButtonWithLabel("CancelBtn", "Cancel current Delivery", buttonRow.transform, Color.red, 300, 90f,18,Color.black);
            cancelButton = cancelBtn;
            cancelLabel = cancelLbl;
            if (!QuestDelivery.QuestActive)
            ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");


// Accept Button (separate row)
            var (acceptGO, acceptBtn, acceptLbl) = UIFactory.RoundedButtonWithLabel("AcceptBtn", "No quest selected", rightPanel.transform, new Color32(0x91,0xFF,0x8E,0xff), 460f, 60f,22,Color.black);
            acceptButton = acceptBtn;
            acceptLabel = acceptLbl;
            ButtonUtils.Disable(acceptBtn, acceptLabel, "No quest selected");



            LoadQuests();
        }

        private void RefreshButton()
        {
            RefreshQuestList();
            LoadQuests();
            ConsoleHelper.RunCashCommand(-50000);
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

                if (def is WeedDefinition)
                {
                    mafiaLabel = "Client: German Mafia";
                }
                else if (def is MethDefinition)
                {
                    mafiaLabel = "Client: Canadian Mafia";
                }
                else if (def is CocaineDefinition)
                {
                    mafiaLabel = "Client: Russian Mafia";
                }

                

                Sprite iconSprite = def.Icon;

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
        private void CancelCurrentQuest(QuestData quest)
        {
            var active = QuestDelivery.Active;
            if (active == null)
            {
                MelonLogger.Warning("❌ No active QuestDelivery found to cancel.");
                deliveryStatus.text = "❌ No active delivery to cancel.";
                return;
            }

            MelonLogger.Msg($"Active quest : {active.Data.ProductID} ");

            

            try
            {
                active.ForceCancel();
                deliveryStatus.text = "🚫 Delivery canceled.";
                ButtonUtils.Disable(cancelButton, cancelLabel, "Canceled");
                ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
                RefreshQuestList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ CancelCurrentQuest() exception: {ex}");
                deliveryStatus.text = "❌ Cancel failed.";
            }
        }



        private void OnSelectQuest(QuestData quest)
        {
            questTitle.text = quest.Title;
            questTask.text = $"Task: {quest.Task}";
            questReward.text = $"Reward: <color=#00FF00>${quest.Reward:N0}</color>";
            deliveryStatus.text = "";
            if (!QuestDelivery.QuestActive)
            {
                ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
                ButtonUtils.ClearListeners(acceptButton);
                ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
            }

            if (QuestDelivery.QuestActive)
            {
                ButtonUtils.Enable(acceptButton, acceptLabel, "In Progress");
                ButtonUtils.ClearListeners(acceptButton);
                ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
                ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel Current Delivery");
                ButtonUtils.ClearListeners(cancelButton);
                ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));
            }

            //ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");
            ButtonUtils.ClearListeners(cancelButton);
            ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));
            ButtonUtils.Enable(refreshButton, refreshLabel, "Refresh Order List");
            ButtonUtils.ClearListeners(refreshButton);
            ButtonUtils.AddListener(refreshButton, () => RefreshButton());

        }

        private void AcceptQuest(QuestData quest)
        {
            string questKey = $"{quest.ProductID}_{quest.AmountRequired}";

            if (QuestDelivery.QuestActive)
            {
                ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(0x91,0xFF,0x8E,0xff));
                deliveryStatus.text = "⚠️ Finish your current job first!";
                ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");

                return;
            }

            if (QuestDelivery.CompletedQuestKeys.Contains(questKey))
            {
                ButtonUtils.SetStyle(acceptButton, acceptLabel, "Already Delivered", new Color32(0x91,0xFF,0x8E,0xff));
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
                Contacts.Buyer?.SendDeliveryAccepted(delivery.Data.ProductID, (int)delivery.Data.RequiredAmount);

                QuestDelivery.Active = delivery; // ✅ FIX: set Active manually here
            }

            ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(0x91,0xFF,0x8E,0xff));


            deliveryStatus.text = "📦 Delivery started!";
            acceptButton.interactable = false;
            ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel Current Delivery");
        }


    }
}