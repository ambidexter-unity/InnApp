using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

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

        public InApp()
        {
            _products = CalculateProductPreInitialization();
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

            foreach (var product in _products)
            {
                builder.AddProduct(product.Id, product.ProductType, new IDs()
                    {
                        {product.GoogleBundle, GooglePlay.Name},
                        {product.IOSBundle, AppleAppStore.Name}
                    });
            }

            UnityPurchasing.Initialize(this, builder);

            return process;
        }

        public IInAppProcess Buy(string idProduct)
        {
            var process = new InAppProcess();

            Predicate<Product> predicate = predicateProduct => predicateProduct.id == idProduct;

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

        private List<Product> CalculateProductPreInitialization()
        {
            //заглушка
            List<Product> result = new List<Product>();

            result.ForEach(product => product.icon = GetSpriteWithId(product.id));

            return result;
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
        // идентификатор внутри приложения 
        public string id;
        public string Id => id;

        // имя товара по умолчанию
        public string title;
        public string Title => title;

        // описание по умолчанию
        public string description;
        public string Description => description;

        // код покупки на маркете AppStore
        public string iOSBundle;
        public string IOSBundle => iOSBundle;

        // код покупки на маркете PlayMarket
        public string googleBundle;
        public string GoogleBundle => googleBundle;

        // тип покупки (расходуемый, нерасходуемый, подписка)
        public ProductType productType;
        public ProductType ProductType => productType;

        // статус приобретения на данный момент
        public bool isBuy;
        public bool IsBuy => isBuy;

        // иконка
        public Sprite icon;
        public Sprite Icon => icon;
    }

    public interface IProduct
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }
        string IOSBundle { get; }
        string GoogleBundle { get; }
        ProductType ProductType { get; }
        bool IsBuy { get; }
        Sprite Icon { get; }
    }
}