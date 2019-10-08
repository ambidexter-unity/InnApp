using System;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections.Generic;
using Injection;
using System.Collections;
using System.Linq;

namespace Drawer.Controller
{
    public class IAPController : MonoController<IAPController>, IStoreListener, iIAPController
    {
        [Inject] protected IAPData IAPData;
        [Inject] protected InternetCheckData InternetCheckData;
        [Inject] protected iSaveController SaveController;
        [Inject] protected iSoftCurrencyController SoftCurrencyController;

        #region iIAPController

        void iIAPController.Buy(string id)
        {
            if (IAPData.PurchaseInProgress == true) return;
            if (IAPData.IsInitialized == false) return;

            IAPData.EventOnPurchaseComplete += IAPData_EventOnPurchaseCompleteHandler;

            IAPData.StoreController.InitiatePurchase(IAPData.StoreController.products.WithID(id));
            IAPData.PurchaseInProgress = true;
            IAPData.OnPurchaseInProgress(IAPData.PurchaseInProgress);
        }

        void iIAPController.RestorePurchases()
        {
            IAPData.RestoreInProgress = true;
            IAPData.AppleExtensions.RestoreTransactions(OnTransactionsRestored);
        }

        void iIAPController.InitizializationSceneBank()
        {
            if (IAPData.IsInitialized)
            {
                InitilizationSceneBank();
            }
            else
            {
                DefaultInitilizationSceneBank();
                IAPData.EventOnInit += IAPData_EventOnInitHandler_InitizializationBank_Handler;
            }
        }

        void iIAPController.DeInitizializationSceneBank()
        {
            IAPData.EventOnInit -= IAPData_EventOnInitHandler_InitizializationBank_Handler;
        }

        bool iIAPController.CheckProductIsBy(string id) => ProductIsBuy(id);

        #endregion

        #region Handlers 

        private void IAPData_EventOnInitHandler_InitizializationBank_Handler(bool succes)
        {
            if (succes)
            {
                IAPData.EventOnInit -= IAPData_EventOnInitHandler_InitizializationBank_Handler;
                InitilizationSceneBank();
            }
        }

        private void DefaultInitilizationSceneBank()
        {
            List<UI.PurchaseButton> buttons = GameObject.FindObjectsOfType<UI.PurchaseButton>().ToList();

#if IAP_DEBUG
            Debug.Log("DefaultInitizializationSceneBank()");
            Debug.Log("Finded PurchaseButtons: " + buttons?.Count);
#endif

            buttons?.ForEach(button =>
            {
                string id = button.productName.ToString();
                button.Description.text = GetDefaultDescription(id);
                button.Price.text = GetDefaultPrice(id);
                button.interactable = false;
            });
        }

        private void InitilizationSceneBank()
        {
            List<UI.PurchaseButton> buttons = GameObject.FindObjectsOfType<UI.PurchaseButton>().ToList();

#if IAP_DEBUG
            Debug.Log("InitizializationSceneBank()");
            Debug.Log("Finded PurchaseButtons: " + buttons?.Count);
#endif

            buttons?.ForEach(button =>
            {
                string id = button.productName.ToString();
                button.Description.text = GetDescription(id);
                button.Price.text = GetPrice(id);
                button.interactable = ProductIsBuy(id) == false;
            });
        }

        private void InternetCheckData_EventOnEndConnectionHandler(bool internetState)
        {
            if (internetState == true)
            {
                InternetCheckData.EventOnEndConnection -= InternetCheckData_EventOnEndConnectionHandler;

                var module = StandardPurchasingModule.Instance();
                module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
                var builder = ConfigurationBuilder.Instance(module);
                PopulateProducts(builder);

                IAPData.EventOnInit += IAPData_EventOnInit_SaveValuesHandler;
                UnityPurchasing.Initialize(this, builder);
                //The result is passed to the event IStoreListener.OnInitialized or IStoreListener.OnInitializeFailed
            }
        }

        private void IAPData_EventOnInit_SaveValuesHandler(bool succes)
        {
            if (succes)
            {
                IAPData.EventOnInit -= IAPData_EventOnInit_SaveValuesHandler;

                List<string> identifiers = IAPData
                                             .StoreController
                                             .products
                                             .all
                                             .Select(prod => prod.definition.id)
                                             .ToList();

                foreach (string id in identifiers)
                {
                    if (GetProductType(id) == ProductType.Subscription)
                    {
                        string keyPurchaseDate = IAPData.PLAYER_PREFS_DATE_BY_SUBSCRIPTIONS + id;
                        string purchaseDate = string.Empty;
                        string defaultDateTimeString = DateTime.MinValue.ToUniversalTime().ToString();

                        Product product = GetPostInitProduct(id);

                        if (product.hasReceipt)
                        {
                            Dictionary<string, string> introductory_info_dict = IAPData.AppleExtensions.GetIntroductoryPriceDictionary();

                            //string intro_json = (introductory_info_dict == null || !introductory_info_dict.ContainsKey(product.definition.storeSpecificId)) ? IAPData.GooglePlayStoreExtensions.H : introductory_info_dict[product.definition.storeSpecificId];

                            //SubscriptionInfo info = new SubscriptionManager(product, intro_json).getSubscriptionInfo();
                            SubscriptionInfo info = new SubscriptionManager(product, product.receipt).getSubscriptionInfo();
#if IAP_DEBUG
                            Debug.Log(string.Join(" \n ", new[]
                                        {
                                            "product id is: " + info.getProductId(),
                                            "purchase date is: " + info.getPurchaseDate(),
                                            "subscription next billing date is: " + info.getExpireDate(),
                                            "is subscribed? " + info.isSubscribed().ToString(),
                                            "is expired? " + info.isExpired().ToString(),
                                            "is cancelled? " + info.isCancelled(),
                                            "product is in free trial peroid? " + info.isFreeTrial(),
                                            "product is auto renewing? " + info.isAutoRenewing(),
                                            "subscription remaining valid time until next billing date is: " + info.getRemainingTime(),
                                            "is this product in introductory price period? " + info.isIntroductoryPricePeriod(),
                                            "the product introductory localized price is: " + info.getIntroductoryPrice(),
                                            "the product introductory price period is: " + info.getIntroductoryPricePeriod(),
                                            "the number of product introductory price period cycles is: " + info.getIntroductoryPricePeriodCycles()
                                        }));
#endif
                            //TODO: check and make sure that when you cancel the subscription and return the money check is not returned.
                            purchaseDate = info.getPurchaseDate().ToUniversalTime().ToString();
                        }
                        else
                        {
                            purchaseDate = SaveController.LoadStringFromPlayerPrefs(IAPData.PLAYER_PREFS_DATE_BY_SUBSCRIPTIONS + id, defaultDateTimeString);
                        }

                        SaveController.SaveStringToPlayerPrefs(keyPurchaseDate, purchaseDate);
                    }
#if IAP_DEBUG
                    Debug.Log(string.Join("\n", new[]
                                         {
                                            "product id: " + id,
                                            "isBy: " + ProductIsBuy(id).ToString(),
                                            "description: " + GetDescription(id).ToString(),
                                            "price: " + GetPrice(id).ToString()
                                        }));
#endif

                    string keyIsBy = IAPData.PLAYER_PREFS_IS_BY + id;
                    bool valueIsBy = ProductIsBuy(id);
                    SaveController.SaveBoolToPlayerPrefs(keyIsBy, valueIsBy);

                    string keyDescription = IAPData.PLAYER_PREFS_DESRIPTION + id;
                    string valueDescription = GetDescription(id);
                    SaveController.SaveStringToPlayerPrefs(keyDescription, valueDescription);

                    string keyPrice = IAPData.PLAYER_PREFS_PRICE + id;
                    string valuePrice = GetPrice(id);
                    SaveController.SaveStringToPlayerPrefs(keyPrice, valuePrice);
                }

                PlayerPrefs.Save();
            }
        }

        private void IAPData_EventOnPurchaseCompleteHandler(string id)
        {
            IAPData.EventOnPurchaseComplete -= IAPData_EventOnPurchaseCompleteHandler;
            SaveController.SaveBoolToPlayerPrefs(IAPData.PLAYER_PREFS_IS_BY + id, true);

            if (GetProductType(id) == ProductType.Subscription)
            {
                string keyPurchaseDate = IAPData.PLAYER_PREFS_DATE_BY_SUBSCRIPTIONS + id;
                string purchaseDate = DateTime.Now.ToUniversalTime().ToString();

                SaveController.SaveStringToPlayerPrefs(keyPurchaseDate, purchaseDate);
            }

            GetActionPostSuccesPurchase(id).Invoke();
            InitilizationSceneBank();
        }

        #endregion

        #region Private Methods

        //private void Start() => StartInitializationWithDelay(IAPData.FIRST_DELAY_INITILIZATION);

        private void StartInitializationWithDelay(float delay) => StartCoroutine(StartInitilizationCoroutineDelay(delay));
        private IEnumerator StartInitilizationCoroutineDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (InternetCheckData.InternetState)
            {
                InternetCheckData_EventOnEndConnectionHandler(true);
            }
            else
            {
                InternetCheckData.EventOnEndConnection += InternetCheckData_EventOnEndConnectionHandler;
            }
        }

        private string GetPrice(string id) => GetPostInitProduct(id)?.metadata.localizedPriceString ?? GetDefaultPrice(id);

        private string GetDefaultPrice(string id)
        {
            string playerPrefsKey = IAPData.PLAYER_PREFS_PRICE + id;
            string defaultValue = GetPreInitProduct(id).DefaultPrice;
            return SaveController.LoadStringFromPlayerPrefs(playerPrefsKey, defaultValue);
        }

        private string GetDescription(string id)
        {
            string description = GetPostInitProduct(id)?.metadata.localizedDescription ?? string.Empty;
            return string.IsNullOrEmpty(description) ? GetDefaultDescription(id) : description;
        }

        private string GetDefaultDescription(string id)
        {
            string playerPrefsKey = IAPData.PLAYER_PREFS_DESRIPTION + id;
            string defaultValue = GetPreInitProduct(id).DefaultDescription;
            return SaveController.LoadStringFromPlayerPrefs(playerPrefsKey, defaultValue);
        }

        private bool ProductIsBuy(string id)
        {
            bool result = default(bool);

            switch (GetProductType(id))
            {
                case ProductType.Consumable:
                    result = false;
                    break;
                case ProductType.NonConsumable:
                    result = SaveController.LoadBoolFromPlayerPrefs(IAPData.PLAYER_PREFS_IS_BY + id);
                    break;
                case ProductType.Subscription:
                    result = SubscriptionIsBy(id);
                    break;
            }

            return result;
        }

        private ProductType GetProductType(string id) => GetPreInitProduct(id).ProductType;

        private PreInitProduct GetPreInitProduct(string id) => IAPData.PreInitProducts.Find(element => element.BaseIdString == id);

        private Product GetPostInitProduct(string id) => IAPData.StoreController.products.WithID(id);

        private void OnTransactionsRestored(bool success)
        {
            IAPData.RestoreInProgress = false;
            IAPData.OnRestorePurchases(success);
            Debug.Log("Transactions restored.");
        }

        private void PopulateProducts(ConfigurationBuilder builder)
        {
            foreach (PreInitProduct item in IAPData.PreInitProducts)
            {
                builder.AddProduct(item.BaseIdString, item.ProductType, new IDs()
                {
                    {item.IOSBundle, AppleAppStore.Name},
                    {item.GoogleBundle, GooglePlay.Name},
                });
            }
        }

        private bool SubscriptionIsBy(string id)
        {
            string defaultDateTimeString = DateTime.MinValue.ToUniversalTime().ToString();
            string dateBySubString = SaveController.LoadStringFromPlayerPrefs(IAPData.PLAYER_PREFS_DATE_BY_SUBSCRIPTIONS + id, defaultDateTimeString);
            DateTime dateBySub = Convert.ToDateTime(dateBySubString);

            bool result = (DateTime.Now - dateBySub).Days < IAPData.DAY_ITERATION_SUBSCRIPTION;

            return result;
        }

        private Action GetActionPostSuccesPurchase(string id)
        {
            Action result;
            ProductName productName;

            if (Enum.TryParse<ProductName>(id, out productName))
            {
                switch (productName)
                {
                    case ProductName.SmallPack:
                        result = () =>
                        {
                            SoftCurrencyController.Sum2Stars(20);
                            Debug.Log("Buy purchase: SmallPack");
                        };
                        break;
                    case ProductName.AveragePack:
                        result = () =>
                        {
                            SoftCurrencyController.Sum2Stars(50);
                            Debug.Log("Buy purchase: AveragePack");
                        };
                        break;
                    case ProductName.BigPack:
                        result = () =>
                        {
                            SoftCurrencyController.Sum2Stars(100);
                            Debug.Log("Buy purchase: BigPack");
                        };
                        break;
                    case ProductName.Subscription:
                        result = () =>
                        {
                            SaveController.SaveBoolToPlayerPrefs(IAPData.PLAYER_PREFS_IS_BY + id, true);
                            SaveController.SaveStringToPlayerPrefs(IAPData.PLAYER_PREFS_DATE_BY_SUBSCRIPTIONS + id, DateTime.Now.ToUniversalTime().ToString());
                            Debug.Log("Buy purchase: Subscription");
                        };
                        break;
                    case ProductName.None:
                    default:
                        result = () => Debug.LogError("Unknown purchase error!");
                        break;
                }
            }
            else
            {
                result = () => Debug.LogError("Unknown purchase error!");
            }

            return result;
        }

        #endregion

        #region Purchase Behavior

        #endregion

        #region IStoreListener

        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            IAPData.StoreController = controller;
            IAPData.AppleExtensions = extensions.GetExtension<IAppleExtensions>();
            IAPData.AppleExtensions.RegisterPurchaseDeferredListener(item => Debug.Log("Purchase iOS deferred: " + item.definition.id));

            IAPData.IsInitialized = true;

            IAPData.OnInit(true);
            Debug.Log("IAP initilization SUCCES");
        }

        void IStoreListener.OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.Log("Billing failed to initialize!");
            switch (error)
            {
                case InitializationFailureReason.PurchasingUnavailable:
                    Debug.Log("Billing disabled!");
                    break;
                case InitializationFailureReason.NoProductsAvailable:
                    Debug.Log("No products available for purchase!");
                    break;
                case InitializationFailureReason.AppNotKnown:
                    Debug.Log("Is your App correctly uploaded on the relevant publisher console?");
                    break;
            }

            if (InternetCheckData.InternetState == false)
            {
                //90 % - это сбой интернета во время инициализациии. Запускаем заново с небольшой заддержкой.
                StartInitializationWithDelay(IAPData.PERIODIC_DELAY_INITILIZATION);
            }

            IAPData.IsInitialized = false;
            Debug.Log("isInitialized false");
            IAPData.OnInit(false);
        }

        PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs e)
        {
            string id = e.purchasedProduct.definition.id;

            Debug.Log("Purchase OK: " + id);
            Debug.Log("Receipt: " + e.purchasedProduct.receipt);

            IAPData.PurchaseInProgress = false;
            IAPData.OnPurchaseProcess(id);
            IAPData.OnPurchaseComplete(id);
            IAPData.OnPurchaseInProgress(IAPData.PurchaseInProgress);

            return PurchaseProcessingResult.Complete;
        }

        void IStoreListener.OnPurchaseFailed(Product item, PurchaseFailureReason reuqest)
        {
            string id = item.definition.id;

            if (reuqest == PurchaseFailureReason.UserCancelled)
            {
                IAPData.OnPurchaseUserCancel(id);
                Debug.Log("Purchase user cancelled: " + id);
            }
            else
            {
                IAPData.OnPurchaseError(id);
                Debug.Log("Purchase error: " + id + " " + reuqest.ToString());
            }

            IAPData.PurchaseInProgress = false;
            IAPData.OnPurchaseInProgress(false);
        }

        #endregion
    }
}

public interface iIAPController : IInjectable
{
    void Buy(string idPurchase);
    void RestorePurchases();

    bool CheckProductIsBy(string id);

    void InitizializationSceneBank();
    void DeInitizializationSceneBank();
}

﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPData : Data
{
    // Константы для работы с сохранениями
    private const string PLAYER_PREFS_KEY = "IAP_key_";
    public const string PLAYER_PREFS_IS_BY = PLAYER_PREFS_KEY + "is_by";
    public const string PLAYER_PREFS_DATE_BY_SUBSCRIPTIONS = PLAYER_PREFS_KEY + "Day_by_sub";
    public const string PLAYER_PREFS_DESRIPTION = PLAYER_PREFS_KEY + "Description";
    public const string PLAYER_PREFS_PRICE = PLAYER_PREFS_KEY + "Price";
    // Остальные константы
    public const int DAY_ITERATION_SUBSCRIPTION = 31;
    public const float FIRST_DELAY_INITILIZATION = 1f;
    public const float PERIODIC_DELAY_INITILIZATION = 5f;

    public bool PurchaseInProgress;
    public bool IsInitialized;
    public bool RestoreInProgress;

    public IStoreController StoreController;
    public IAppleExtensions AppleExtensions;
    public IGooglePlayStoreExtensions GooglePlayStoreExtensions;

    public event Action<bool> EventOnInit;
    public void OnInit(bool isSuccesConnection) => EventOnInit?.Invoke(isSuccesConnection);

    public event Action<string> EventOnPurchaseComplete;
    public void OnPurchaseComplete(string id) => EventOnPurchaseComplete?.Invoke(id);

    public event Action<string> EventOnPurchaseProcess;
    public void OnPurchaseProcess(string id) => EventOnPurchaseProcess?.Invoke(id);

    public event Action<string> EventOnPurchaseUserCancel;
    public void OnPurchaseUserCancel(string id) => EventOnPurchaseUserCancel?.Invoke(id);

    public event Action<string> EventOnPurchaseError;
    public void OnPurchaseError(string id) => EventOnPurchaseError?.Invoke(id);

    public event Action<bool> EventOnRestorePurchases;
    public void OnRestorePurchases(bool result) => EventOnRestorePurchases?.Invoke(result);

    public event Action<bool> EventOnPurchaseInProgress;
    public void OnPurchaseInProgress(bool progress) => EventOnPurchaseInProgress?.Invoke(progress);

    private List<PreInitProduct> _preInitProducts = null;
    public List<PreInitProduct> PreInitProducts
    {
        get
        {
            if (_preInitProducts == null)
            {
                _preInitProducts = new List<PreInitProduct>()
                {
                     new PreInitProduct(
                       baseId: ProductName.Subscription,
                       iOSiD: "ios.amdidexterplus.subsription",
                       googleBundle : "amdidexterplus.subsription",
                       defaultDescription: "Opens all the coloring!",
                       defaultPrice: "loading...",
                       productType: ProductType.Subscription),

                     new PreInitProduct(
                       baseId: ProductName.SmallPack,
                       iOSiD: "amdidexterplus.smallpack",
                       googleBundle : "amdidexterplus.smallpack",
                       defaultDescription: "20",
                       defaultPrice: "loading...",
                       productType: ProductType.Consumable),

                     new PreInitProduct(
                       baseId: ProductName.AveragePack,
                       iOSiD: "amdidexterplus.averagepack",
                       googleBundle : "amdidexterplus.averagepack",
                       defaultDescription: "50",
                       defaultPrice: "loading...",
                       productType: ProductType.Consumable),

                     new PreInitProduct(
                       baseId: ProductName.BigPack,
                       iOSiD: "amdidexterplus.bigpack",
                       googleBundle : "amdidexterplus.bigpack",
                       defaultDescription: "100",
                       defaultPrice: "loading...",
                       productType: ProductType.Consumable),
                };
            }
            return _preInitProducts;
        }
    }
}

public struct PreInitProduct
{
    private ProductName baseId;
    public ProductName BaseId => baseId;

    public string BaseIdString => baseId.ToString();

    private string iOSiD;
    public string IOSBundle => iOSiD;

    private string googleBundle;
    public string GoogleBundle => googleBundle;

    private string defaultDescription;
    public string DefaultDescription => defaultDescription;

    private string defaultPrice;
    public string DefaultPrice => defaultPrice;

    private ProductType productType;
    public ProductType ProductType => productType;

    public PreInitProduct(ProductName baseId, string iOSiD, string googleBundle, string defaultDescription, string defaultPrice, ProductType productType)
    {
        this.baseId = baseId;
        this.iOSiD = iOSiD;
        this.googleBundle = googleBundle;
        this.defaultDescription = defaultDescription;
        this.defaultPrice = defaultPrice;
        this.productType = productType;
    }
}

public enum ProductName
{
    None,
    Subscription,
    SmallPack,
    AveragePack,
    BigPack
}
