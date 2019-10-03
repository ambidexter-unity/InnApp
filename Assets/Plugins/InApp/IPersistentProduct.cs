using UnityEngine;
using UnityEngine.Purchasing;

namespace IAP
{
    public interface IPersistentProduct
    {
        bool ReadOnlyIsValid { get; }
        ProductNames BaseID { get; }
        ProductType ProductType { get; }
        string ReadOnlyDescription { get; }
        string ReadOnlyPrice { get; }
        bool ReadOnlyIsBuy { get; }
		int HoursCooldownPurchased { get; }
		string ReadOnlyPurchase { get; }
		Sprite ReadOnlyIcon { get; }
        string ReadOnlyTitle { get; }
    }
}