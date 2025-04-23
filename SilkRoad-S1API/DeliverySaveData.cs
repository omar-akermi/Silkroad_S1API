using Newtonsoft.Json;
using System;

[Serializable]
public class DeliverySaveData
{
    public string ProductID;
    public uint RequiredAmount;
    public int Reward;
    public string DeliveryDropGUID;
    public string RewardDropGUID;
    public bool Initialized;

    [JsonConstructor]
    public DeliverySaveData() { }
}
