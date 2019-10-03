using Model;
using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Zenject;

namespace IAP
{
    public class IAPHandlers
    {
#pragma warning disable 649
        [Inject] protected IIAPModel _iapModel;
        [Inject] protected GameModelController _gameModelController;
#pragma warning restore 649

        [Inject]
        private void Construct()
        {
            _iapModel.PurchaseEventsStream
                .Where(next => next.Item1 == PurchasingEvents.SuccesComplete)
                .Subscribe(next =>
                {
                    switch (next.Item2)
                    {
                        case ProductNames.gold20:
                            _gameModelController.AddIAPPurchaseHardCurrency(20);
                            break;
                        case ProductNames.gold110:
                            _gameModelController.AddIAPPurchaseHardCurrency(110);
                            break;
                        case ProductNames.gold620:
                            _gameModelController.AddIAPPurchaseHardCurrency(620);
                            break;
                        case ProductNames.gold2000:
                            _gameModelController.AddIAPPurchaseHardCurrency(2000);
                            break;
                        case ProductNames.gold4500:
                            _gameModelController.AddIAPPurchaseHardCurrency(4500);
                            break;
                        case ProductNames.vip1day:
                            EnableVipBoost(next.Item2);
                            break;
                        case ProductNames.vip3days:
                            EnableVipBoost(next.Item2);
                            break;
                        case ProductNames.vip7days:
                            EnableVipBoost(next.Item2);
                            break;
                        default:
                            break;
                    }
                });
        }

        private void EnableVipBoost(ProductNames name)
        {
            //GameModelController при формирования множителя для заработанного софта сама проверяет, куплены или нет вип-бусты и не закончился ли у них срок активации. (GameModelController.GetFactorForAddSoftCurrency())
            //Но на всякий случай не удаляю ниже закомиченное, т.к. изменение модель-контроллера при покупки можно сделать напрямую отсюда, для наглядности

            //Predicate<IPersistentProduct> matchPredicate = persistentProduct => persistentProduct.BaseID == name;
            //TimeSpan errorTimeSpan = TimeSpan.FromDays(7);
            //TimeSpan hoursVIPMode;
            //var products = _iapModel.ValidPersistentProducts;
            //if (products.Exists(matchPredicate))
            //{
            //    var matchProduct = products.Find(matchPredicate);
            //    if (matchProduct.HoursCooldownPurchased > 0)
            //    {
            //        hoursVIPMode = TimeSpan.FromHours(matchProduct.HoursCooldownPurchased);
            //    }
            //    else
            //    {
            //        hoursVIPMode = errorTimeSpan;
            //        Debug.LogError("Unknown error");
            //    }
            //}
            //else
            //{
            //    hoursVIPMode = errorTimeSpan;
            //    Debug.LogError("Unknown error");
            //}

            //_gameModelController.EnableVIPBoost(DateTime.Now + hoursVIPMode);
        }
    }
}