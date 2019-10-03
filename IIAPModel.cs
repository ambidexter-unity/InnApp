using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace IAP
{
    public interface IIAPModel
    {
        bool CheckCanDoAnyOperation();

        IObservable<bool> RestoredPurchasesStream { get; }

        IObservable<Tuple<PurchasingEvents, ProductNames>> PurchaseEventsStream { get; }

        bool IsInitialized { get; }

        IObservable<bool> InitializedStream { get; }

        List<IPersistentProduct> ValidPersistentProducts { get; }
    }
}