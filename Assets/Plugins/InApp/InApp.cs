using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InApp
{
    public class InApp : IStoreListener
    {
        private bool _isInit = false;
        public bool IsInit => _isInit;

        private List<Product> _products = new List<Product>();
        public List<IProduct> Product => _products.Select(product => product as IProduct).ToList();

        private IStoreController _storeController;
        private IAppleExtensions _appleExtensions;

        private event Action<Result> _initEvent;
        private event Action<string, Result> _purchasingEvent; //(id продукта, результат)

        private readonly string _keyForCheckSave;
        private readonly string _keyForTitle;
        private readonly string _keyForDescription;
        private readonly string _keyForPrice;
        private readonly string _keyForCheckIsBy;

        public InApp(TranslationLocale locale)
        {
            // ключи для PlayerPrefs
            var temp = new Product(string.Empty);
            string _baseKey = nameof(InApp);
            _keyForCheckSave = _baseKey + nameof(temp.IsBuy);
            _keyForTitle = _baseKey + nameof(temp.Title);
            _keyForDescription = _baseKey + nameof(temp.Description);
            _keyForPrice = _baseKey + nameof(temp.Price);
            _keyForCheckIsBy = _baseKey + nameof(temp.IsBuy);

            Dictionary<int, decimal> appStorePriceTiers = new Dictionary<int, decimal>(); //написать класс парсер

            bool isAndroid = false;
#if UNITY_EDITOR
            isAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
#elif UNITY_IPHONE
            isAndroid = false;
#elif UNITY_ANDROID
            isAndroid = true;
#else
            isAndroid = true;
#endif

            // заполняем продукты из сохраненных данных - по умолчанию или из PlayerPrefs
            var defaultCatalog = ProductCatalog.LoadDefaultCatalog();
            ProductCatalogItem a; //delete

            string decimalFormatingStyle = "F2";
            string paymentCurrency = "USD"; //для дефолтных значений ценник всегда в долларах, особенность реализации ProductCatalog

            foreach (var defaultProduct in defaultCatalog.allValidProducts)
            {
                bool productIsSave = PlayerPrefs.GetString(_keyForCheckSave + defaultProduct.id, false.ToString()) == true.ToString();

                var product = new Product(defaultProduct.id);
                if (productIsSave)
                {
                    //формируем продукт из PlayerPrefs
                    product.isBuy = product.productType == ProductType.Consumable
                    ? false
                    : PlayerPrefs.GetString(_keyForCheckIsBy + defaultProduct.id, false.ToString()) == true.ToString();
                }
                else
                {
                    //формируем продукт ProductCatalog (дефолтные значения)
                    var translation = defaultProduct.GetDescription(locale); //добавить проверку и выставление в дефолт
                    product.title = translation.Title;
                    product.description = translation.Description;
                    string price = string.Empty;
                    if (isAndroid)
                    {
                        price = defaultProduct.googlePrice.value.ToString(decimalFormatingStyle);
                    }
                    else
                    {
                        int tier = defaultProduct.applePriceTier;
                        if (appStorePriceTiers.ContainsKey(tier))
                        {
                            price = appStorePriceTiers[tier].ToString(decimalFormatingStyle);
                        }
                        else
                        {
                            Debug.LogError($"Ошибка при поиске ценника для Appstore по словарю appStorePriceTiers " +
                                $"для продукта {product.Id}: В словаре нет такого ключа.");

                            price = "9.99";
                        }
                    }

                    product.price = $"{price} {paymentCurrency}";
                    product.isBuy = false;

                    //сохраняем продукт в PlayerPrefs'ы
                    SaveProductToPlayerPrefs(product);
                }

                product.productType = defaultProduct.type;
                product.MarketIds = new IDs();
                defaultProduct.allStoreIDs.ToList().ForEach(marketId => product.MarketIds.Add(marketId.id, marketId.store));
                product.icon = GetSpriteWithId(product.Id);
            }
        }

        private void SaveProductToPlayerPrefs(IProduct product, params string[] keys)
        {
            if (keys.Length > 0)
            {
                foreach (string key in keys)
                {
                    if (key == _keyForCheckSave) PlayerPrefs.SetString(_keyForCheckSave + product.Id, true.ToString());
                    else if (key == _keyForTitle) PlayerPrefs.SetString(_keyForTitle + product.Id, product.Title);
                    else if (key == _keyForDescription) PlayerPrefs.SetString(_keyForDescription + product.Id, product.Description);
                    else if (key == _keyForPrice) PlayerPrefs.SetString(_keyForPrice + product.Id, product.Price);
                    else if (key == _keyForCheckIsBy) PlayerPrefs.SetString(_keyForCheckIsBy + product.Id, product.IsBuy.ToString());
                    else Debug.LogError($"Попытка сохранить параметр продукта {product.Id} по ключу {key}, " +
                        $"которого нет среди возможных к сохранению");
                }
            }
            else
            {
                PlayerPrefs.SetString(_keyForCheckSave + product.Id, true.ToString());
                PlayerPrefs.SetString(_keyForTitle + product.Id, product.Title);
                PlayerPrefs.SetString(_keyForDescription + product.Id, product.Description);
                PlayerPrefs.SetString(_keyForPrice + product.Id, product.Price);
                PlayerPrefs.SetString(_keyForCheckIsBy + product.Id, product.IsBuy.ToString());
            }
        }

        public IInAppProcess Inizialization()
        {
            var process = new InAppProcess();

            void initHandler(Result result)
            {
                if (result == Result.Succes)
                    RefreshProducts();

                process.result = result;
                process.isDone = true;

                _initEvent -= initHandler;
            }
            _initEvent += initHandler;

            var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
            module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
#endif
            var builder = ConfigurationBuilder.Instance(module);

            _products.ForEach(product => builder.AddProduct(product.Id, product.ProductType, product.MarketIds));

            UnityPurchasing.Initialize(this, builder);

            return process;
        }

        public IInAppProcess Buy(string idProduct)
        {
            var process = new InAppProcess();

            Predicate<Product> predicate = predicateProduct => predicateProduct.Id == idProduct;

            if (_products.Exists(predicate) == false)
            {
                Debug.LogError($"Trying to start buying a product that is not in the catalog. Product id: { idProduct}");
                process.result = Result.Fail;
                process.isDone = true;
                return process;
            }

            var product = _products.Find(predicate);

            void purchaseHandler(string id, Result result)
            {
                if (idProduct == id && result == Result.Succes)
                {
                    //действия при удачной покупке

                    switch (product.ProductType)
                    {
                        case ProductType.Consumable:
                            break;
                        case ProductType.NonConsumable:
                            product.isBuy = true;
                            break;
                        case ProductType.Subscription:
                            break;
                        default:
                            break;
                    }
                }

                process.result = result;
                process.isDone = true;

                _purchasingEvent -= purchaseHandler;
            }
            _purchasingEvent += purchaseHandler;

            _storeController.InitiatePurchase(_storeController.products.WithID(idProduct));

            return process;
        }

        private void RefreshProducts()
        {
            //заглушка
        }

        private Sprite GetSpriteWithId(string id)
        {
            //заглушка
            return (Sprite)new object();
        }

        #region IStoreListener

        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _storeController = controller;

            _appleExtensions = extensions.GetExtension<IAppleExtensions>();
            _appleExtensions.RegisterPurchaseDeferredListener(item => Debug.Log("Purchase iOS deferred: " + item.definition.id));

            _initEvent?.Invoke(Result.Succes);
            Debug.Log("IAP Initialized succes");
        }

        void IStoreListener.OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogWarning("Billing failed to initialize!");
            switch (error)
            {
                case InitializationFailureReason.PurchasingUnavailable:
                    Debug.LogWarning("Billing disabled!");
                    break;
                case InitializationFailureReason.NoProductsAvailable:
                    Debug.LogWarning("No products available for purchase!");
                    break;
                case InitializationFailureReason.AppNotKnown:
                    Debug.LogWarning("Is your App correctly uploaded on the relevant publisher console?");
                    break;
            }

            _initEvent?.Invoke(Result.Fail);
            Debug.LogWarning("IAP Initialized filed");
        }

        PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs argument)
        {
            string id = argument.purchasedProduct.definition.id;

            Debug.Log("Purchase succes: " + id);
            Debug.Log("Receipt: " + argument.purchasedProduct.receipt);

            _purchasingEvent?.Invoke(id, Result.Succes);
            return PurchaseProcessingResult.Complete;
        }

        void IStoreListener.OnPurchaseFailed(UnityEngine.Purchasing.Product unityProduct, PurchaseFailureReason reuqest)
        {
            string id = unityProduct.definition.id;
            Result result;

            if (reuqest == PurchaseFailureReason.UserCancelled)
            {
                result = Result.Other;
                Debug.Log("Purchase user cancelled: " + id);
            }
            else
            {
                result = Result.Fail;
                Debug.Log("Purchase error: " + id + " " + reuqest.ToString());
            }

            _purchasingEvent?.Invoke(id, result);
        }
        #endregion
    }

    public class InAppProcess : IInAppProcess
    {
        public bool isDone = false;
        public bool IsDone => IsDone;

        public Result result = Result.Other;
        public Result Result => result;
    }

    public interface IInAppProcess
    {
        bool IsDone { get; }
        Result Result { get; }
    }

    public enum Result
    {
        Succes,
        Fail,
        Other
    }

    public class Product : IProduct
    {
        private string _id;
        public string Id => _id;

        public string title;
        public string Title => title;

        public string description;
        public string Description => description;

        public string price;
        public string Price => price;

        public ProductType productType;
        public ProductType ProductType => productType;

        public bool isBuy;
        public bool IsBuy => isBuy;

        public Sprite icon;
        public Sprite Icon => icon;

        public IDs MarketIds;

        public Product(string id)
        {
            _id = id;
        }
    }

    public interface IProduct
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }
        ProductType ProductType { get; }
        string Price { get; }
        bool IsBuy { get; }
        Sprite Icon { get; }
    }
}