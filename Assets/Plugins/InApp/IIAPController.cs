using Common.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IAP
{
    public interface IIAPController : IGameService
    {
        void Buy(ProductNames idPurchase);

        void AppleRestoreTransactions();
    }
}