using Common.PersistentManager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace IAP
{
    [Serializable]
    public class PersistentProduct : IPersistent<PersistentProduct>, IPersistentProduct
    {
        [SerializeField] public bool IsValid;
        public bool ReadOnlyIsValid => IsValid;

        [SerializeField] private int _baseID;
        public ProductNames BaseID => (ProductNames)_baseID;
        string IPersistent<PersistentProduct>.PersistentId => BaseID.ToString();

        [SerializeField] private string _iOSiD;
        public string IOSBundle => _iOSiD;

        [SerializeField]
        private string _googleBundle;
        public string GoogleBundle => _googleBundle;

        [SerializeField]
        public string Title;
        public string ReadOnlyTitle => Title;

        [SerializeField] private ProductType _productType;
        public ProductType ProductType => _productType;

        [SerializeField] public string Description;
        public string ReadOnlyDescription => Description;

        [SerializeField] public string Price;
        public string ReadOnlyPrice => Price;

        [SerializeField] public bool IsBuy;
        public bool ReadOnlyIsBuy => CheckIsBy();

        [SerializeField] public string DatePurchased;
        public string ReadOnlyPurchase => DatePurchased;

        [SerializeField] private int _hoursCooldownPurchased;
        public int HoursCooldownPurchased => _hoursCooldownPurchased;

		public Sprite Icon;
		public Sprite ReadOnlyIcon => Icon;

		public PersistentProduct() { }

        public PersistentProduct(ProductNames baseId) => _baseID = (int)baseId;

        public PersistentProduct(DefaultPurchaseParameters parameters, Common.Locale.ILocaleService localizator)
        {
			_baseID = (int)parameters.BaseId;
            _iOSiD = parameters.IOSBundle;
            _googleBundle = parameters.GoogleBundle;
            _productType = parameters.ProductType;
            Title = localizator.GetLocalized(parameters.DefaultTitle);
            Description = localizator.GetLocalized(parameters.DefaultDescription);
            Price = localizator.GetLocalized(parameters.DefaultPrice);
            IsBuy = false;
            _hoursCooldownPurchased = parameters.HoursCooldownPurchased;
            IsValid = true;
        }

        void IPersistent<PersistentProduct>.Restore<T1>(T1 data)
        {
            var persistProduct = data as PersistentProduct;
            _baseID = (int)persistProduct.BaseID;
            _iOSiD = persistProduct.IOSBundle;
            _googleBundle = persistProduct.GoogleBundle;
            _productType = persistProduct.ProductType;
            Title = persistProduct.ReadOnlyTitle;
            Price = persistProduct.ReadOnlyPrice;
            Description = persistProduct.ReadOnlyDescription;
            IsBuy = persistProduct.ReadOnlyIsBuy;
            IsValid = persistProduct.ReadOnlyIsValid;

			if (string.IsNullOrEmpty(persistProduct.ReadOnlyPurchase) == false)
                DatePurchased = persistProduct.ReadOnlyPurchase;

            _hoursCooldownPurchased = persistProduct.HoursCooldownPurchased;
        }

        /// <summary>
        /// Проверяет, куплено или нет с учетом типа продукта
        /// </summary>
        private bool CheckIsBy()
        {
            bool result;

            switch (_productType)
            {
                case ProductType.NonConsumable:
                    result = IsBuy;
                    break;
                case ProductType.Consumable:
                    if (string.IsNullOrEmpty(DatePurchased))
                    {
                        result = false;
                    }
                    else
                    {
                        if (_hoursCooldownPurchased > 0)
                        {
                            DateTime datePurchased = Convert.ToDateTime(DatePurchased);
                            result = (DateTime.Now - datePurchased).TotalHours < _hoursCooldownPurchased;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    break;
                case ProductType.Subscription:
                    // Логика подписок не описана в этой реализации.
                    Debug.LogError("Unknown error");
                    result = false;
                    break;
                default:
                    result = false;
                    break;
            }

            return result;
        }

        public override string ToString()
        {
           return string.Join(", ", new[]
            {
                "product id: " + BaseID.ToString(),
                "isBy: " + ReadOnlyIsBuy,
                "isValid: " + ReadOnlyIsValid,
                "title: " + ReadOnlyTitle,
                "description: " + ReadOnlyDescription,
                "price: " + ReadOnlyPrice,
				"icon: " + ReadOnlyIcon?.name ?? "none",
                "datePurchasing: " +  (string.IsNullOrEmpty(ReadOnlyPurchase)
                ?  "not by" : ReadOnlyPurchase.ToString())
            });
        }
    }
}