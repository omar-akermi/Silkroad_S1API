using System;
using System.Linq;
using UnityEngine;
using MelonLoader;
using S1API.Items;
using S1API.Money;
using S1API.Storages;
using S1API.DeadDrops;
using S1API.Quests;
using S1API.Products;
using S1API.Saveables;
using S1API.NPCs;
using System.Collections.Generic;
using S1API.GameTime;
using S1API.Internal.Utils;
using S1API.PhoneApp;
using S1API.Utils;
using Random = UnityEngine.Random;

namespace SilkRoad
{
    public class QuestDelivery : Quest
    {
        [SaveableField("DeliveryData")]
        public DeliverySaveData Data = new DeliverySaveData();

        private DeadDropInstance deliveryDrop;
        public static HashSet<string> CompletedQuestKeys = new HashSet<string>();

        private QuestEntry deliveryEntry;
        private QuestEntry rewardEntry;
        public static bool QuestActive = false;
        public static event Action OnQuestCompleted;
        protected override Sprite? QuestIcon => ImageUtils.LoadImage("..\\..\\Mods\\silkroad\\SilkroadIcon_quest.png");
        protected override void OnLoaded()
        {
            base.OnLoaded();
            MelonCoroutines.Start(WaitForBuyerAndSendStatus());
        }

        private System.Collections.IEnumerator WaitForBuyerAndSendStatus()
        {
            float timeout = 5f;
            float waited = 0f;

            while ((Contacts.Buyer == null || !Contacts.Buyer.IsInitialized) && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null; // wait 1 frame
            }

            if (Contacts.Buyer == null || !Contacts.Buyer.IsInitialized)
            {
                MelonLogger.Warning("⚠️ Buyer NPC still not initialized after timeout. Skipping status sync.");
                yield break;
            }
            
        }
        
        protected override void OnCreated()
        {
            base.OnCreated();
            QuestActive = true;
            
            MelonLogger.Msg($"🔍 QuestDelivery CreateInternal: ProductID={Data?.ProductID}, Initialized={Data?.Initialized}");

            if (Data == null)
            {
                MelonLogger.Error("❌ QuestDelivery.Data is null!");
                Data = new DeliverySaveData();
            }

            if (!Data.Initialized)
            {
                var drops = DeadDropManager.All?.ToList();
                if (drops == null || drops.Count < 6)
                {
                    MelonLogger.Error("❌ Not enough dead drops to assign delivery/reward.");
                    return;
                }

                deliveryDrop = drops[RandomUtils.RangeInt(0, drops.Count)];
                //deliveryDrop = drops[5];

                Data.DeliveryDropGUID = deliveryDrop.GUID; Data.Initialized = true;
                
            }
            else
            {
                deliveryDrop = DeadDropManager.All.FirstOrDefault(d => d.GUID == Data.DeliveryDropGUID);

                if (deliveryDrop == null )
                {
                    MelonLogger.Warning("⚠️ Failed to resolve saved DeadDrops. Reassigning...");
                    var drops = DeadDropManager.All.ToList();

                    if (drops.Count >= 2)
                    {
                        deliveryDrop = drops[0];
                        Data.DeliveryDropGUID = deliveryDrop.GUID;
                    }
                    else
                    {
                        MelonLogger.Error("❌ Not enough DeadDrops to reassign.");
                        return;
                    }
                    //S1Quest.onQuestBegin?.Invoke();

                }
            }

            deliveryEntry = AddEntry($"Deliver {Data.RequiredAmount}x bricks of {Data.ProductID} at the dead drop.");
            deliveryEntry.POIPosition = deliveryDrop.Position;
            deliveryEntry.Begin();
            rewardEntry = AddEntry($"Wait for the payment to arrive.");
            rewardEntry.SetState(QuestState.Inactive);

            deliveryDrop.Storage.OnClosed += CheckDelivery;

            //Contacts.Buyer?.SendDeliveryAccepted(Data.ProductID, (int)Data.RequiredAmount);

            MelonLogger.Msg("📦 QuestDelivery started with drop locations assigned.");
        }

        // Add this field to your QuestDelivery class
        private bool rewardGiven = false;

        private void CheckDelivery()
        {
            MelonLogger.Msg("CheckDelivery called.");
            MelonLogger.Msg($"Expecting ProductID: {Data.ProductID}, RequiredAmount: {Data.RequiredAmount}");

            foreach (var slot in deliveryDrop.Storage.Slots)
            {
                bool isProductInstance = slot.ItemInstance is ProductInstance;
                string slotProductID = isProductInstance ? ((ProductInstance)slot.ItemInstance).Definition.Name : "null";
                string packaging = isProductInstance ? ((ProductInstance)slot.ItemInstance).AppliedPackaging.Name : "null";
                int quantity = slot.Quantity;

                MelonLogger.Msg($"Slot: isProductInstance={isProductInstance}, productID={slotProductID}, packaging={packaging}, quantity={quantity}");
            }

            var total = deliveryDrop.Storage.Slots
                .Where(slot => slot.ItemInstance is ProductInstance product &&
                       product.AppliedPackaging.Name == "Brick" &&
                       product.Definition.Name == Data.ProductID)
                .Sum(slot => slot.Quantity);

            MelonLogger.Msg($"Total bricks found that match criteria: {total}");

            if (total < Data.RequiredAmount)
            {
                MelonLogger.Msg($"❌ Not enough bricks: {total}/{Data.RequiredAmount}");
                return;
            }

            uint toRemove = Data.RequiredAmount;
            foreach (var slot in deliveryDrop.Storage.Slots)
            {
                if (slot.ItemInstance is ProductInstance product &&
                    product.AppliedPackaging.Name == "Brick" &&
                    product.Definition.Name == Data.ProductID)
                {
                    int remove = (int)Mathf.Min(slot.Quantity, toRemove);
                    MelonLogger.Msg($"Removing {remove} from slot with {slot.Quantity} bricks (before), Product: {product.Definition.Name}, Packaging: {product.AppliedPackaging.Name}");
                    slot.AddQuantity(-remove);
                    toRemove -= (uint)remove;
                    MelonLogger.Msg($"Slot now has {slot.Quantity} bricks (after). Bricks left to remove: {toRemove}");
                    if (toRemove == 0) break;
                }
            }

            deliveryEntry.Complete();
            rewardEntry.SetState(QuestState.Active);
            rewardGiven = false; // Reset flag before starting coroutine and subscribing
            MelonCoroutines.Start(DelayedReward());

            // Subscribe to OnDayPass when reward entry becomes active
            TimeManager.OnDayPass += TimeManager_OnDayPass;

            Contacts.Buyer?.SendDeliverySuccess(Data.ProductID);

            MelonLogger.Msg("✅ Delivery complete. Reward entry now active.");
        }

        // Add a method to handle the OnDayPass event
        private void TimeManager_OnDayPass()
        {
            TryGiveReward("OnDayPass");
        }

        // Modify the DelayedReward coroutine:
        private System.Collections.IEnumerator DelayedReward()
        {
            float delaySeconds = (float)RandomUtils.RangeInt(120,200);
            yield return new WaitForSeconds(delaySeconds);

            TryGiveReward("Delay");
        }

        // Add this new method to handle awarding the reward safely
        private void TryGiveReward(string source)
        {
            if (rewardGiven) return; // Prevent double reward
            rewardGiven = true;
            // Unsubscribe from OnDayPass when reward is given
           TimeManager.OnDayPass -= TimeManager_OnDayPass;

            if (deliveryEntry == null)
                return;

            var rewardAmount = Data.Reward;

            ConsoleHelper.RunCashCommand(rewardAmount);

            MelonLogger.Msg($"💵 Player rewarded with ${rewardAmount} using Console.ChangeCashCommand. Source: {source}");

            QuestActive = false;
            string key = $"{Data.ProductID}_{Data.RequiredAmount}";
            CompletedQuestKeys.Add(key);
            Contacts.Buyer?.SendRewardDropped();
            rewardEntry?.Complete();
            Complete();
            OnQuestCompleted?.Invoke();
        }
        protected override string Title =>
            Data?.ProductID != null ? $"Deliver {Data.ProductID}" : "Silkroad Delivery";

        protected override string Description =>
            Data?.ProductID != null && Data.RequiredAmount > 0
                ? $"Deliver {Data.RequiredAmount}x bricks of {Data.ProductID} to the drop point."
                : "Deliver the assigned product to the stash location.";


    }
}