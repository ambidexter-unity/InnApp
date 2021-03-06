using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
#if UNITY_EDITOR
using UnityEditor;
#endif
using InAppPurchasing.SubClasses;
//Обратите внимание, что используются типы с одинаковыми названиями в пространствах InAppPurchasing и UnityEngine.Purchasing
//Ряд типов и перечислений продублировано в пользовательском пространстве имен, чтобы сокрыть UnityEngine.Purchasing
using Product = InAppPurchasing.SubClasses.Product;
using Debug = UnityEngine.Debug;
using ProductType = UnityEngine.Purchasing.ProductType;
using TranslationLocale = UnityEngine.Purchasing.TranslationLocale;
using System.Diagnostics;

namespace InAppPurchasing
{
    public class InApp : IStoreListener
    {
        private bool _isInit = false;
        public bool IsInit => _isInit;

        private List<Product> _products = new List<Product>();
        public List<IProduct> Product => _products.Select(product => product as IProduct).ToList();

        private IStoreController _storeController;
#if UNITY_IPHONE
        private IAppleExtensions _appleExtensions;
#endif

        private event Action<Result> _initEvent;
        private event Action<string, Result, string> _purchasingEvent; //(id продукта, результат, чек)

        private readonly string _keyForCheckSave;
        private readonly string _keyForTitle;
        private readonly string _keyForDescription;
        private readonly string _keyForPrice;
        private readonly string _keyForCheckIsBy;

        private const string STRING_MODIFICATOR_FOR_PRICE = "F2";

        private Dictionary<char, string> currencySignsToTranscripts = new Dictionary<char, string>()
        {
            ['₴'] = "UAH",
            ['$'] = "USD",
            ['€'] = "EUR",
            ['£'] = "GBP",
            ['¥'] = "JPY",
            ['¥'] = "CNY",
            ['₽'] = "RUB",
            ['₪'] = "ILS",
            ['₨'] = "INR",
            ['₩'] = "KRW",
            ['₦'] = "NGN",
            ['฿'] = "THB",
            ['₫'] = "VND",
            ['₭'] = "LAK",
            ['៛'] = "KHR",
            ['₮'] = "MNT",
            ['₱'] = "PHP",
            ['﷼'] = "IRR",
            ['₡'] = "CRC",
            ['₲'] = "PYG",
            ['؋'] = "AFN",
            ['₵'] = "GHS",
            ['₸'] = "KZT",
            ['₺'] = "TRY",
            ['₼'] = "AZN",
            ['₾'] = "GEL"
        };

        private readonly UnityEngine.Purchasing.TranslationLocale _locale;

        private IEnumerator DelayAction(float second, Action action)
        {
            yield return new WaitForSecondsRealtime(second);
            action?.Invoke();
        }

        public InApp(TranslationLocale locale)
        {
            _locale = (UnityEngine.Purchasing.TranslationLocale)(int)locale;

            // ключи для PlayerPrefs
            var temp = new Product(string.Empty);
            string _baseKey = nameof(InApp);
            _keyForCheckSave = _baseKey + "IsSave";
            _keyForTitle = _baseKey + nameof(temp.Title);
            _keyForDescription = _baseKey + nameof(temp.Description);
            _keyForPrice = _baseKey + nameof(temp.Price);
            _keyForCheckIsBy = _baseKey + nameof(temp.IsBuy);

            bool isAndroid = false;
#if UNITY_EDITOR
            isAndroid = (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) == false;
#elif UNITY_IPHONE
            isAndroid = false;
#elif UNITY_ANDROID
            isAndroid = true;
#else
            isAndroid = true;
#endif

            // подтягиваем ценники для appstore для iphone
            Dictionary<int, decimal> appStorePriceTiers = null;
            if (isAndroid == false)
                appStorePriceTiers = new AppStoreListTiersCreator().GetValues();

            // находим и сетим иконки для продуктов
            Dictionary<string, Sprite> productIcons = null;
            ProductIcons productIconsContainer = Resources.Load<ProductIcons>(ProductIcons.RESOURCES_PATH);
            if (productIconsContainer != null)
            {
                productIcons = productIconsContainer.GetProductIconsList();
            }
            else
            {
                Debug.LogError($"Не найден файл по пути {ProductIcons.RESOURCES_PATH} !");
            }

            string decimalFormatingStyle = STRING_MODIFICATOR_FOR_PRICE;
            string paymentCurrency = "RUB";

            // заполняем продукты из сохраненных данных - по умолчанию или из PlayerPrefs
            var defaultCatalog = ProductCatalog.LoadDefaultCatalog();

            foreach (var defaultProduct in defaultCatalog.allValidProducts)
            {
                var product = new Product(defaultProduct.id);

                bool productIsSave = PlayerPrefs.GetString(_keyForCheckSave + product.Id, false.ToString()) == true.ToString();
                if (productIsSave)
                {
                    //формируем продукт из PlayerPrefs
                    product.title = PlayerPrefs.GetString(_keyForTitle + product.Id, string.Empty);
                    product.description = PlayerPrefs.GetString(_keyForDescription + product.Id, string.Empty);
                    product.price = PlayerPrefs.GetString(_keyForPrice + product.Id, string.Empty);
                    product.isBuy = PlayerPrefs.GetString(_keyForCheckIsBy + defaultProduct.id, false.ToString()) == true.ToString();

                    if (new List<string>() { product.title, product.description, product.price, product.isBuy.ToString() }
                    .Exists(parameters => string.IsNullOrEmpty(parameters)) == true)
                        Debug.LogError($"Один из параметров продукта {product.Id} не был корректно сохранен!");
                }
                else
                {
                    //формируем продукт ProductCatalog (дефолтные значения)
                    var translation = defaultProduct.GetDescription(_locale);

                    if (translation == null)
                        translation = defaultProduct.defaultDescription;

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

                // сетим иконку
                if (productIcons != null)
                {
                    if (productIcons.ContainsKey(product.Id))
                    {
                        Sprite icon = productIcons[product.Id];

                        if (icon != null)
                        {
                            product.icon = icon;
                        }
                        else
                        {
                            Debug.LogError($"В словаре {productIcons} по ключу {product.Id} отсутствует значение");
                        }
                    }
                    else
                    {
                        Debug.LogError($"В словаре {productIcons} нет такого ключа, как {product.Id}");
                    }
                }

                _products.Add(product);
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

            //if (_isInit)
            //{
            //    process.result = Result.Succes;
            //    process.isDone = true;
            //    return process;
            //}

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

            _products.ForEach(product => builder.AddProduct(product.Id, (UnityEngine.Purchasing.ProductType)(int)product.ProductType, product.MarketIds));

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

            void purchaseHandler(string id, Result result, string receipt = null)
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

                    SaveProductToPlayerPrefs(product, _keyForCheckIsBy);
                }

                process.result = result;
                process.isDone = true;
                process.argument = receipt;

                _purchasingEvent -= purchaseHandler;
            }
            _purchasingEvent += purchaseHandler;

            _storeController.InitiatePurchase(_storeController.products.WithID(idProduct));

            return process;
        }

        private void RefreshProducts()
        {
            UnityEngine.Purchasing.Product[] unityProducts = _storeController.products.all;

            foreach (var unityProduct in unityProducts)
            {
                string unityIapProductId = unityProduct.definition.storeSpecificId;
                var product = _products.Find(preProduct => preProduct.Id == unityIapProductId);

                product.description = unityProduct.metadata.localizedDescription;
                var title = unityProduct.metadata.localizedTitle;
                //маркет возвращает заголовок с названием игры в скобках почему-то
                var mathChar = '(';
                if (title.Contains(mathChar))
                {
                    var chatNum = title.IndexOf(mathChar);
                    title = title.Remove(chatNum);
                }
                product.title = title;

                var localizedPriceString = unityProduct.metadata.localizedPriceString;
                var localizedPrice = unityProduct.metadata.localizedPrice;

                product.price = ConvertToCorrectStringPrice(localizedPriceString, localizedPrice);

                SaveProductToPlayerPrefs(product, _keyForTitle, _keyForDescription, _keyForPrice);
            }
        }

        private string ConvertToCorrectStringPrice(string stringValue, decimal longValue)
        {
            var result = stringValue;

            char? foundCurrencySymbol = null;

            foreach (var item in stringValue)
            {
                if (foundCurrencySymbol.HasValue)
                {
                    break;
                }

                if (currencySignsToTranscripts.ContainsKey(item))
                {
                    foundCurrencySymbol = item;
                }
            }

            if (foundCurrencySymbol.HasValue)
            {
                result = $"{longValue.ToString(STRING_MODIFICATOR_FOR_PRICE)} {currencySignsToTranscripts[foundCurrencySymbol.Value]}";
            }

            return result;
        }

        #region IStoreListener

        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _storeController = controller;

#if UNITY_IPHONE

            _appleExtensions = extensions.GetExtension<IAppleExtensions>();
            _appleExtensions.RegisterPurchaseDeferredListener(item => Debug.Log("Purchase iOS deferred: " + item.definition.id));
#endif
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
            var receipt = argument.purchasedProduct.receipt;
            var product = _products.Find(_ => _.Id == id);

            _purchasingEvent?.Invoke(id, Result.Succes, receipt);

            Debug.Log("Purchase succes: " + id);
            Debug.Log("Receipt: " + receipt);

            return PurchaseProcessingResult.Complete;
        }

        void IStoreListener.OnPurchaseFailed(UnityEngine.Purchasing.Product unityProduct, PurchaseFailureReason reuqest)
        {
            string id = unityProduct.definition.id;
            var product = _products.Find(_ => _.Id == id);

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

            _purchasingEvent?.Invoke(id, result, null);
        }

        #endregion
    }

    public interface IInAppProcess
    {
        bool IsDone { get; }
        Result Result { get; }
        object Argument { get; }
    }

    public enum Result
    {
        Succes,
        Fail,
        Other
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

    public enum ProductType
    {
        Consumable = 0,
        NonConsumable = 1,
        Subscription = 2
    }

    public enum TranslationLocale
    {
        zh_TW = 0,
        cs_CZ = 1,
        da_DK = 2,
        nl_NL = 3,
        en_US = 4,
        fr_FR = 5,
        fi_FI = 6,
        de_DE = 7,
        iw_IL = 8,
        hi_IN = 9,
        it_IT = 10,
        ja_JP = 11,
        ko_KR = 12,
        no_NO = 13,
        pl_PL = 14,
        pt_PT = 15,
        ru_RU = 16,
        es_ES = 17,
        sv_SE = 18,
        zh_CN = 19,
        en_AU = 20,
        en_CA = 21,
        en_GB = 22,
        fr_CA = 23,
        el_GR = 24,
        id_ID = 25,
        ms_MY = 26,
        pt_BR = 27,
        es_MX = 28,
        th_TH = 29,
        tr_TR = 30,
        vi_VN = 31
    }
}

namespace InAppPurchasing.SubClasses
{
    public class InAppProcess : IInAppProcess
    {
        public bool isDone = false;
        public bool IsDone => isDone;

        public Result result = Result.Other;
        public Result Result => result;

        public object argument;
        public object Argument => argument;
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

        public UnityEngine.Purchasing.ProductType productType;
        public InAppPurchasing.ProductType ProductType => (InAppPurchasing.ProductType)(int)productType;

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

#if UNITY_EDITOR
    public class AnyWindowEditor : EditorWindow
    {
        [MenuItem("Window/Unity IAP/Product Icons", false, 1)]
        private static void OpenWindow()
        {
            ProductIcons asset = AssetDatabase.LoadAssetAtPath<ProductIcons>(ProductIcons.FUUL_PATH);

            if (asset == null)
            {
                asset = ProductIcons.CreateInstance<ProductIcons>();
                AssetDatabase.CreateAsset(asset, ProductIcons.FUUL_PATH);
                AssetDatabase.SaveAssets();
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
#endif

    public class AppStoreListTiersCreator
    {
        // должно быть чтение из файла, но времени не хватило и пока жестко указаны ценики в коде. Ценники обновляются ~раз в год, так сто не особо страшно
        private Dictionary<int, decimal> values = new Dictionary<int, decimal>()
        {
            [0] = 0.00m,
            [1] = 0.99m,
            [2] = 1.99m,
            [3] = 2.99m,
            [4] = 3.99m,
            [5] = 4.99m,
            [6] = 5.99m,
            [7] = 6.99m,
            [8] = 7.99m,
            [9] = 8.99m,
            [10] = 9.99m,
            [11] = 10.99m,
            [12] = 11.99m,
            [13] = 12.99m,
            [14] = 13.99m,
            [15] = 14.99m,
            [16] = 15.99m,
            [17] = 16.99m,
            [18] = 17.99m,
            [19] = 18.99m,
            [20] = 19.99m,
            [21] = 20.99m,
            [22] = 21.99m,
            [23] = 22.99m,
            [24] = 23.99m,
            [25] = 24.99m,
            [26] = 25.99m,
            [27] = 26.99m,
            [28] = 27.99m,
            [29] = 28.99m,
            [30] = 29.99m,
            [31] = 30.99m,
            [32] = 31.99m,
            [33] = 32.99m,
            [34] = 33.99m,
            [35] = 34.99m,
            [36] = 35.99m,
            [37] = 36.99m,
            [38] = 37.99m,
            [39] = 38.99m,
            [40] = 39.99m,
            [41] = 40.99m,
            [42] = 41.99m,
            [43] = 42.99m,
            [44] = 43.99m,
            [45] = 44.99m,
            [46] = 45.99m,
            [47] = 46.99m,
            [48] = 47.99m,
            [49] = 48.99m,
            [50] = 49.99m,
            [51] = 54.99m,
            [52] = 59.99m,
            [53] = 64.99m,
            [54] = 69.99m,
            [55] = 74.99m,
            [56] = 79.99m,
            [57] = 84.99m,
            [58] = 89.99m,
            [59] = 94.99m,
            [60] = 99.99m,
            [61] = 109.9m,
            [62] = 119.99m,
            [63] = 124.99m,
            [64] = 129.99m,
            [65] = 139.99m,
            [66] = 149.99m,
            [67] = 159.99m,
            [68] = 169.99m,
            [69] = 174.99m,
            [70] = 179.99m,
            [71] = 189.99m,
            [72] = 199.99m,
            [73] = 209.99m,
            [74] = 219.99m,
            [75] = 229.99m,
            [76] = 239.99m,
            [77] = 249.99m,
            [78] = 299.99m,
            [79] = 349.99m,
            [80] = 399.99m,
            [81] = 449.99m,
            [82] = 499.99m,
            [83] = 599.99m,
            [84] = 699.99m,
            [85] = 799.99m,
            [86] = 899.99m,
            [87] = 999.99m,
        };

        public Dictionary<int, decimal> GetValues() => values;
    }
}
