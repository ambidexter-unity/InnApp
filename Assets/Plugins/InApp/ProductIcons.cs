using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InApp
{
    public class ProductIcons : ScriptableObject
    {
        public const string FUUL_PATH = "Assets/Resources/ProductIcons.asset";
        public const string RESOURCES_PATH = "ProductIcons.asset";

        [SerializeField]
        private List<ProductIconsItem> _productIconsItems;

        public Dictionary<string, Sprite> GetProductIconsList()
        {
            Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();

            if (_productIconsItems != null)
            {
                foreach (var item in _productIconsItems)
                {
                    result.Add(item.ProductId, item.ProductIcon);
                }
            }

            return result;
        }
    }

    [Serializable]
    public class ProductIconsItem
    {
        [SerializeField]
        private string _productId;
        public string ProductId => _productId;

        [SerializeField]
        private Sprite _productIcon;
        public Sprite ProductIcon => _productIcon;
    }
}