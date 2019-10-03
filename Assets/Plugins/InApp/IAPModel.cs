using Common.PersistentManager;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Purchasing;
using UniRx;
using Zenject;
using System.Linq;

namespace IAP
{
    [Serializable]
    public class IAPModel : IStoreListener, IPersistent<IAPModel>, IIAPModel
    {
        string IPersistent<IAPModel>.PersistentId => nameof(IAPModel);

        // классы для работы с UnityEngine.Purchasing
        private IStoreController _storeController;
        private IAppleExtensions _appleExtensions;

        // список айдишников продуктов
        [SerializeField] private List<int> _persistentProductsIds;
        public List<ProductNames> PersistentProductsIds => _persistentProductsIds.Select(intId => (ProductNames)intId).ToList();

        // список продуктов
        private List<PersistentProduct> _persistentProducts;
        public List<PersistentProduct> PersistentProducts => _persistentProducts;

        // поток для наблюдения за событиями покупок (начало, ошибка, отмена)
        public readonly Subject<Tuple<PurchasingEvents, ProductNames>> InnerPurchaseEventsStream = new Subject<Tuple<PurchasingEvents, ProductNames>>(); // поток для внутренних наблюдателей
        public readonly Subject<Tuple<PurchasingEvents, ProductNames>> OutPurchaseEventsStream = new Subject<Tuple<PurchasingEvents, ProductNames>>();  // итоговый поток инициализации
        public IObservable<Tuple<PurchasingEvents, ProductNames>> PurchaseEventsStream => OutPurchaseEventsStream;

        // поле статуса инициализации
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        // стрим статуса инициализации. 
        public readonly Subject<bool> InnerInitStream = new Subject<bool>(); // поток для внутренних наблюдателей
        public readonly Subject<bool> OutInitStream = new Subject<bool>(); // итоговый поток инициализации
        public IObservable<bool> InitializedStream => OutInitStream;

        // стрим для слежения статуса восстановления покупок
        private readonly Subject<bool> _appleRestoredPurchasesTransactions = new Subject<bool>();
        public IObservable<bool> RestoredPurchasesStream => _appleRestoredPurchasesTransactions;

        // статусы процессов 
        private bool _appleRestoreInProgress = false;
        private bool _purchaseInProgress = false;

        public IAPModel()
        {
            OutInitStream.Subscribe(next => _isInitialized = next);

            InnerPurchaseEventsStream.Subscribe(next => _purchaseInProgress = next.Item1 == PurchasingEvents.StartPurchase);

            // если происходит удачное восстановление покупок, вызывает обработчики удачной покупки расходуемых товаров,
            // только если покупка не была совершена ранее на этом устройстве
            _appleRestoredPurchasesTransactions
                .Subscribe(next => PersistentProducts
                .Where(product => product.ProductType == ProductType.Consumable)
                .Where(product => product.ReadOnlyIsBuy == false)
                .ToList()
                .ForEach(consumbleProduct => InnerPurchaseEventsStream
                    .OnNext(new Tuple<PurchasingEvents, ProductNames>(PurchasingEvents.SuccesComplete, consumbleProduct.BaseID))));
        }

        public void InitiatePurchase(ProductNames id)
        {
            _storeController.InitiatePurchase(_storeController.products.WithID(id.ToString()));
            var notify = new Tuple<PurchasingEvents, ProductNames>(PurchasingEvents.StartPurchase, id);

            InnerPurchaseEventsStream.OnNext(notify);
        }

        public bool CheckCanDoAnyOperation() => _purchaseInProgress == false
                                             && _isInitialized == true
                                             && _appleRestoreInProgress == false;

        public List<IPersistentProduct> ValidPersistentProducts =>
            _persistentProducts
            .Where(product => product.ReadOnlyIsValid)
            .Select(product => product as IPersistentProduct)
            .ToList();

        // пока не нужен
        //public bool CheckPersistentProductsByID(string id) => PersistentProducts.Exists(product => string.Equals(product.BaseID.ToString(), id));

        public PersistentProduct GetPersistentProduct(string baseID) => _persistentProducts.Find(product => string.Equals(product.BaseID.ToString(), baseID));

        public List<Product> GetUnityIapProducts()
        {
            List<Product> result;

            Product[] products = _storeController?.products?.all;

            if (products != null)
            {
                result = products.ToList();
            }
            else
            {
                result = new List<Product>();
            }

            return result;
        }

        void IPersistent<IAPModel>.Restore<T1>(T1 data)
        {
            var src = data as IAPModel;
            Assert.IsNotNull(src);

            _persistentProductsIds = src.PersistentProductsIds.Select(idEnum => (int)idEnum).ToList();
        }

        public void SetDefaultProducts(List<DefaultPurchaseParameters> purchaseParameters, Common.Locale.ILocaleService localizator)
        {
            _persistentProductsIds = new List<int>();
            _persistentProducts = new List<PersistentProduct>();

            foreach (var defaultParameters in purchaseParameters)
            {
                var newPersistentProduct = new PersistentProduct(defaultParameters, localizator);

                _persistentProducts.Add(newPersistentProduct);
                _persistentProductsIds.Add((int)newPersistentProduct.BaseID);
            }
        }

        public void RestorePersistentProducts(List<PersistentProduct> persistentProducts) =>
            _persistentProducts = persistentProducts;

        public void AppleRestoreTransactions()
        {
            if (CheckCanDoAnyOperation() == false) return;

            _appleRestoreInProgress = true;
            _appleExtensions.RestoreTransactions((bool success) =>
            {
                _appleRestoreInProgress = false;
                _appleRestoredPurchasesTransactions.OnNext(success);
                Debug.Log("Transactions restored: " + success);
            });
        }

        public ProductNames? ConvertStringProductToEnum(string productName)
        {
            ProductNames? convertedID = null;

            try
            {
                convertedID = (ProductNames)Enum.Parse(typeof(ProductNames), productName);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }

            return convertedID;
        }

        #region IStoreListener

        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _storeController = controller;

            _appleExtensions = extensions.GetExtension<IAppleExtensions>();
            _appleExtensions.RegisterPurchaseDeferredListener(item => Debug.Log("Purchase iOS deferred: " + item.definition.id));

            InnerInitStream.OnNext(true);
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

            InnerInitStream.OnNext(false);
            Debug.LogWarning("IAP Initialized filed");
        }

        PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs e)
        {
            ProductNames? id = ConvertStringProductToEnum(e.purchasedProduct.definition.id);

            if (id.HasValue)
            {
                Debug.Log("Purchase succes: " + id.Value.ToString());
                Debug.Log("Receipt: " + e.purchasedProduct.receipt);

                var notify = new Tuple<PurchasingEvents, ProductNames>(PurchasingEvents.SuccesComplete, id.Value);
                InnerPurchaseEventsStream.OnNext(notify);
            }
            else
            {
                Debug.LogWarning("Unknown error");
            }

            return PurchaseProcessingResult.Complete;
        }

        void IStoreListener.OnPurchaseFailed(Product item, PurchaseFailureReason reuqest)
        {
            ProductNames? id = ConvertStringProductToEnum(item.definition.id);

            Tuple<PurchasingEvents, ProductNames> notify;

            if (id.HasValue)
            {
                if (reuqest == PurchaseFailureReason.UserCancelled)
                {
                    notify = new Tuple<PurchasingEvents, ProductNames>(PurchasingEvents.UserCancelled, id.Value);
                    Debug.Log("Purchase user cancelled: " + id);
                }
                else
                {
                    notify = new Tuple<PurchasingEvents, ProductNames>(PurchasingEvents.OtherError, id.Value);
                    Debug.Log("Purchase error: " + id + " " + reuqest.ToString());
                }

                InnerPurchaseEventsStream.OnNext(notify);
            }
            else
            {
                Debug.LogWarning("Unknown error");
            }
        }
        #endregion
    }
}