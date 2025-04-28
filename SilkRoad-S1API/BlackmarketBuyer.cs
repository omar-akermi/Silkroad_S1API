using System;
using System.Diagnostics;
using S1API.Entities;
using S1API.Internal.Utils;
using S1API.PhoneApp;
using UnityEngine;


namespace SilkRoad
{
    public class BlackmarketBuyer : NPC
    {
        static Sprite? npcSprite => ImageUtils.LoadImage("silkroad\\SilkroadIcon.png");

        public BlackmarketBuyer() : base("blackmarket_buyer", "Blackmarket", "Buyer", npcSprite )
        {
        }


        private static readonly string[] DeliveryAcceptedTexts =
        {
            "We’ve heard of your product. {amount} bricks of {product}. Impress us.",
            "Your name reached our ears. {amount} bricks of {product}. Discreet drop. No attention.",
            "Quality like yours doesn't go unnoticed. Deliver {amount} bricks of {product}. Same location.",
            "We're watching, and we’re interested. {amount} bricks of  {product}. Clean work only.",
            "Consider this a test. {amount} bricks of {product}. Don’t disappoint."
        };


        private static readonly string[] DeliverySuccessTexts =
        {
            "The stash was verified. Your reward will be with you shortly.",
            "Everything checked out. Sit tight — payment's on the way.",
            "Impressive. Clean drop. Expect your cash soon.",
            "The cargo's in place. Funds are being processed.",
            "Solid work. Your reward is en route. Stay alert."
        };

        private static readonly string[] InstantRewardTexts =
        {
            "Payment’s been sent. You did good — we notice things like that.",
            "Cash is in your account. You pulled your weight, and that matters.",
            "Transfer complete. Always good doing business with someone dependable.",
            "Funds delivered. You handled it right — that earns respect.",
            "You’ve been paid. Stick with us, and there’s more where that came from."
        };

        public bool IsInitialized { get; private set; } = false;

        protected override void OnLoaded()
        {
            base.OnLoaded();

            Contacts.Buyer = this;
            IsInitialized = true;
        }


        protected override void OnCreated()
        {
            base.OnCreated();
            Contacts.Buyer = this;
        }

        public void SendDeliveryAccepted(string product, int amount)
        {
            string line = DeliveryAcceptedTexts[RandomUtils.RangeInt(0, DeliveryAcceptedTexts.Length)];
            string formatted = line
                .Replace("{product}", $"<color=#34AD33>{product}</color>")
                .Replace("{amount}", $"<color=#FF0004>{amount}x</color>");

            SendTextMessage(formatted);
        }

        public void SendDeliverySuccess(string product)
        {
            string line = DeliverySuccessTexts[RandomUtils.RangeInt(0, DeliverySuccessTexts.Length)];
            SendTextMessage(line);
        }

        public void SendRewardDropped()
        {
            string line = InstantRewardTexts[RandomUtils.RangeInt(0, InstantRewardTexts.Length)];
            SendTextMessage(line);
        }
    }
}
