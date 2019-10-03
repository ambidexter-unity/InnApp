using System;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Zenject;
using UniRx;
using Common.PersistentManager;
using Common.Service;
using InternetConnectionChecker;
using Common.Locale;
using System.IO;

namespace IAP
{
    public class IAPController : IIAPController
    {
#pragma warning disable 649
        protected IAPModel _iapModel;
        [Inject] protected InternetCheckController _internetChecker;
        [Inject] private readonly DiContainer _container;
        [Inject] private readonly IPersistentManager _persistentManager;
        [Inject] private readonly IAPSettings _iapSettings;
        [Inject] private readonly ILocaleService _iLocaleService;
#pragma warning restore 649

        private ReactiveProperty<bool> _ready = new ReactiveProperty<bool>(false);
        IReadOnlyReactiveProperty<bool> IGameService.Ready => _ready;

        private CompositeDisposable _disposables = new CompositeDisposable();

        void IGameService.Initialize()
        {
            _iapModel = new IAPModel();

            bool modelIsRestore = _persistentManager.Restore(_iapModel);
            if (modelIsRestore)
            {
                List<PersistentProduct> persistentProducts = new List<PersistentProduct>();

                foreach (var id in _iapModel.PersistentProductsIds)
                {
                    PersistentProduct persistentProduct = new PersistentProduct(id);
                    _persistentManager.Restore(persistentProduct);
                    persistentProducts.Add(persistentProduct);
                }

                _iapModel.RestorePersistentProducts(persistentProducts);
            }
            else
            {
                _iapModel.SetDefaultProducts(_iapSettings.PurchaseParameters, _iLocaleService);
                _iapModel.PersistentProducts.ForEach(persistenProduct => _persistentManager.Persist(persistenProduct));
                _persistentManager.Persist(_iapModel);
            }

            _iapModel.PersistentProducts.ForEach(persistentProducet =>
            {
                ProductNames baseId = persistentProducet.BaseID;
                Predicate<DefaultPurchaseParameters> matchPredicate = defaultProduct => defaultProduct.BaseId == baseId;

                if (_iapSettings.PurchaseParameters.Exists(matchPredicate))
                {
                    var icon = _iapSettings.PurchaseParameters.Find(matchPredicate).Icon;

                    if (icon != null)
                    {
                        persistentProducet.Icon = icon;
                    }
                    else
                    {
                        Debug.LogWarning($"У продукта с идентификатором {baseId.ToString()} в IAPSettings не засечена иконка");
                    }
                }
                else
                {
                    Debug.LogError($"Продукта с идентификатором {baseId.ToString()} нет в IAPSettings");
                }
            });

            _container.Bind<IIAPModel>().FromInstance(_iapModel).AsSingle();

            _container.Inject(new IAPHandlers());

            //StartInitilization();

            _ready.SetValueAndForceNotify(true);
        }
        private void StartInitilization()
        {
            if (_internetChecker.InternetConnection.Value)
            {
                var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
                module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
#endif
                var builder = ConfigurationBuilder.Instance(module);

                foreach (var product in _iapModel.PersistentProducts)
                {
                    builder.AddProduct(product.BaseID.ToString(), product.ProductType, new IDs()
                    {
                        {product.GoogleBundle, GooglePlay.Name},
                        {product.IOSBundle, AppleAppStore.Name}
                    });
                }

                IDisposable disposeAwaitIAPInit = null;
                disposeAwaitIAPInit = _iapModel.InnerInitStream
                    .Subscribe(resultInit =>
                    {
                        _disposables.Remove(disposeAwaitIAPInit);
                        EndInitilization(resultInit);
                    }).AddTo(_disposables);

                UnityPurchasing.Initialize(_iapModel, builder);
            }
            else
            {
                IDisposable disposeAwaitInternet = null;
                disposeAwaitInternet = _internetChecker.InternetConnection
                    .First(next => next == true)
                    .Subscribe(next =>
                    {
                        _disposables.Remove(disposeAwaitInternet);
                        StartInitilization();
                    }).AddTo(_disposables);
            }
        }

        private void EndInitilization(bool initSuccess)
        {
            if (initSuccess)
            {
                List<PersistentProduct> validedProducts = new List<PersistentProduct>();

                var persistentProducts = _iapModel.PersistentProducts;

                //Debug.Log($"_iapModel.GetUnityIapProducts().Count: {_iapModel.GetUnityIapProducts().Count}");

                _iapModel.GetUnityIapProducts()
                 .ForEach(product =>
                 {
                     string unityIapProdictId = product.definition.storeSpecificId;
                     //Debug.Log($"unityIapProdictId: {unityIapProdictId}");

                     Predicate<PersistentProduct> matchPredicate = checkPersistentProduct => checkPersistentProduct.IOSBundle == unityIapProdictId
                     || checkPersistentProduct.GoogleBundle == unityIapProdictId || checkPersistentProduct.BaseID.ToString() == unityIapProdictId;

#if UNITY_EDITOR
                     if (product.availableToPurchase && persistentProducts.Exists(matchPredicate))
                     {
                         var persistentProduct = persistentProducts.Find(matchPredicate);

                         persistentProduct.Description = product.metadata.localizedDescription;

                         //маркет возвращает символ, которого нет в наших шрифтах
                         var price = product.metadata.localizedPriceString;
                         var mathCharInPrice = '₽';
                         if (price.Contains(mathCharInPrice))
                         {
                             var chatNum = price.IndexOf(mathCharInPrice);
                             price = price.Remove(chatNum, 1);
                             price += "RUB";
                         }

                         var mathCharInPrice2 = ",00";
                         if (price.Contains(mathCharInPrice2))
                         {
                             var chatNum = price.IndexOf(mathCharInPrice2);
                             price = price.Remove(chatNum, mathCharInPrice2.Count());
                         }
                         persistentProduct.Price = price;


                         //маркет возвращает заголовок с названием игры в скобках почему-то
                         var title = product.metadata.localizedTitle;
                         var mathCharInTitle = '(';
                         if (title.Contains(mathCharInTitle))
                         {
                             var chatNum = title.IndexOf(mathCharInTitle);
                             title = title.Remove(chatNum);
                         }
                         persistentProduct.Title = title;

                         //Помещаем в список валидных продуктов
                         validedProducts.Add(persistentProduct);
                         //Debug.Log("add to validateProducts: " + persistentProduct.BaseID);
                     }
#else

                     if (product.availableToPurchase && persistentProducts.Exists(matchPredicate))
                     {
                         var persistentProduct = persistentProducts.Find(matchPredicate);

                         persistentProduct.Description = product.metadata.localizedDescription;

                         var title = product.metadata.localizedTitle;
                         //маркет возвращает заголовок с названием игры в скобках почему-то
                         var mathChar = '(';
                         if (title.Contains(mathChar)) 
                         {
                             var chatNum = title.IndexOf(mathChar);
                             title = title.Remove(chatNum);
                         }
                         persistentProduct.Title = title;

                         //маркет возвращает символ, которого нет в наших шрифтах
                         var price = product.metadata.localizedPriceString;
                         var mathCharInPrice = '₽';
                         if (price.Contains(mathCharInPrice))
                         {
                             var chatNum = price.IndexOf(mathCharInPrice);
                             price = price.Remove(chatNum, 1);
                             price += "RUB";
                         }

                         var mathCharInPrice2 = ",00";
                         if (price.Contains(mathCharInPrice2))
                         {
                             var chatNum = price.IndexOf(mathCharInPrice2);
                             price = price.Remove(chatNum, mathCharInPrice2.Count());
                         }
                         persistentProduct.Price = price;


                         persistentProduct.Price = price;

                         //Помещаем в список валидных продуктов
                         validedProducts.Add(persistentProduct);
                         //Debug.Log("add to validateProducts: " + persistentProduct.BaseID);
                     }
#endif
                 });
                // Помечаем все продукты в приложении, которых маркет возвращает availableToPurchase == false как невалидные. Если ситуация при следующей инициализации изменится обратно, эти продукты снова станут доступны
                _iapModel.PersistentProducts.ToList().ForEach(persistentProduct =>
                    persistentProduct.IsValid = validedProducts.Contains(persistentProduct));

                _persistentManager.Persist(_iapModel);
                _iapModel.PersistentProducts.ForEach(product => _persistentManager.Persist(product));
                _iapModel.PersistentProducts
                    .Where(product => product.IsValid)
                    .ToList()
                    .ForEach(product => Debug.Log(product));
            }
            else
            {
                IDisposable disposeDelayInit = null;
                TimeSpan delaySpan = TimeSpan.FromSeconds(_iapSettings.PeriodicDelayInitilization);
                disposeDelayInit = Observable.Timer(delaySpan)
                    .ObserveOnMainThread()
                    .Subscribe(next =>
                    {
                        _disposables.Remove(disposeDelayInit);
                        StartInitilization();
                    }).AddTo(_disposables);
            }

            _iapModel.OutInitStream.OnNext(initSuccess);
        }

        public void Buy(ProductNames id)
        {
            _iapModel.InitiatePurchase(id);

            IDisposable disposeEndPurchasing = null;
            disposeEndPurchasing = _iapModel.InnerPurchaseEventsStream
                .First(next => next.Item1 != PurchasingEvents.StartPurchase)
                .Subscribe(next =>
                {
                    if (next.Item1 == PurchasingEvents.SuccesComplete)
                    {
                        PersistentProduct product = _iapModel.GetPersistentProduct(next.Item2.ToString());

                        switch (product.ProductType)
                        {
                            case ProductType.Consumable:
                                product.DatePurchased = DateTime.Now.ToUniversalTime().ToString();
                                product.IsBuy = true;
                                break;
                            case ProductType.NonConsumable:
                                product.DatePurchased = DateTime.Now.ToUniversalTime().ToString();
                                break;
                            case ProductType.Subscription:
                                Debug.LogWarning("Подписки в этой реализации не предусмотрены");
                                break;
                            default:
                                break;
                        }

                        _persistentManager.Persist(product);
                    }

                    _iapModel.OutPurchaseEventsStream.OnNext(next);
                    _disposables.Remove(disposeEndPurchasing);
                }).AddTo(_disposables);
        }

        public void AppleRestoreTransactions() => _iapModel.AppleRestoreTransactions();
    }
}