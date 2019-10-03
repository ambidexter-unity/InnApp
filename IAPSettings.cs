using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using UnityEngine;
using UnityEngine.Purchasing;
using Zenject;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IAP
{
    public class IAPSettings : ScriptableObjectInstaller<IAPSettings>
    {
#pragma warning disable 649
        [SerializeField] private float _periodicDelayInitilization = 5f;
        [Space(5)]
        [SerializeField] private List<DefaultPurchaseParameters> _purchaseParameters = new List<DefaultPurchaseParameters>();
#pragma warning restore 649
        public float PeriodicDelayInitilization => _periodicDelayInitilization;


        //TODO: добавить проверку на дубликаты.
        public List<DefaultPurchaseParameters> PurchaseParameters =>_purchaseParameters.ToList();

        public DefaultPurchaseParameters GetParametersForName(ProductNames productName)
        {
	        return _purchaseParameters.FirstOrDefault(parameters => parameters.BaseId == productName);
        }

        public override void InstallBindings() => Container.Bind<IAPSettings>().FromInstance(this).AsSingle();

#if UNITY_EDITOR
        private const string ManagerPath = "Assets/Scripts/Common/Manager";

        [MenuItem("Tools/Game Settings/IAP Settings")]
        private static void GetAndSelectSettingsInstance()
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject =
                InspectorExtensions.FindOrCreateNewScriptableObject<IAPSettings>(ManagerPath);
        }
#endif
    }

    [Serializable]
    public class DefaultPurchaseParameters
    {
#pragma warning disable 649
        [SerializeField] private ProductNames _baseId;
        [SerializeField] private string _iOSiD;
        [SerializeField] private string _googleBundle;
        [SerializeField] private string _defaultTitle;
        [SerializeField] private string _defaultDescription;
        [SerializeField] private string _defaultPrice;
        [SerializeField] private ProductType _productType;
        [Tooltip("Только для расходуемых покупок. Время в часах, когда продукт становится " +
            "снова доступным после удачной покупки")]
        [SerializeField] private int _hoursCooldownPurchased;
		[SerializeField] private Sprite _icon;
#pragma warning restore 649

		public ProductNames BaseId => _baseId;
        public string IOSBundle => _iOSiD;
        public string GoogleBundle => _googleBundle;
        public string DefaultTitle => _defaultTitle;
        public string DefaultDescription => _defaultDescription;
        public string DefaultPrice => _defaultPrice;
        public ProductType ProductType => _productType;

        public int HoursCooldownPurchased => _hoursCooldownPurchased;
		public Sprite Icon => _icon;
	}
}