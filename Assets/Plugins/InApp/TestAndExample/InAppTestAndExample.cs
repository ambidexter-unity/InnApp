using InAppPurchasing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//Для тестов и примера. Ряд конвенций по написанию кода опущено для улучшения наглядности.
public class InAppTestAndExample : MonoBehaviour
{
    public GameObject ExampleProductItem;
    public GameObject GridLayot;

    private Dictionary<string, GameObject> _productItems = new Dictionary<string, GameObject>();

    private void Start()
    {
        //Создаем экземпляр, передавая в конструктор текущик язык. На этом этапе уже подтянутся все дефолтные или сохраненные продукты с последними параметрами
        InApp inApp = new InApp(TranslationLocale.ru_RU);

        // Работать с продуктами можно до начала инициализации
        List<IProduct> products = inApp.Product;

        foreach (var product in products)
        {
            // Берем параметры до инициализации и заполняем обьекты в ui
            string id = product.Id;
            string title = product.Title;
            string description = product.Description;
            string price = product.Price;
            Sprite icon = product.Icon;
            ProductType productType = product.ProductType;

            var uiProduct = GameObject.Instantiate<GameObject>(ExampleProductItem, GridLayot.transform);
            uiProduct.gameObject.SetActive(true);

            Text textTitle = uiProduct.GetComponentsInChildren<Text>()[0];
            Text textDescription = uiProduct.GetComponentsInChildren<Text>()[1];
            Text textPrice = uiProduct.GetComponentsInChildren<Text>()[2];
            Button buttonBuy = uiProduct.GetComponentInChildren<Button>();
            Image imageIcon = uiProduct.GetComponentInChildren<Image>();

            textTitle.text = title;
            textDescription.text = description;
            textPrice.text = price;
            buttonBuy.onClick.AddListener(() =>
            {
                Debug.Log($"Начата покупка товара {product.Id}");
                //При начале покупки получаем обьект, благодаря котором можем следить за процессом и результатом покупки.
                //Перед началом покупки желательно проверить интернет-соединение (здесь опущено)
                IInAppProcess buyProcess = inApp.Buy(product.Id);
                StartCoroutine(PurchasingObserveCoroutine(buyProcess, product));
            });
            buttonBuy.interactable = inApp.IsInit && product.IsBuy == false;
            imageIcon.sprite = icon;

            _productItems.Add(product.Id, uiProduct);
        }

        // Начинаем инициализацию. Перед ней желательно проверить состояние интернет-соединения (здесь опущено)
        // Получаем обьект IInappProcess, благодаря которому можем следить за процессом и результатом инициализации
        var initProcess = inApp.Inizialization();
        // следим за ходом через коррутину (можно другим способом, как вам удобней)
        StartCoroutine(InitInAppObserveCoroutione(initProcess, inApp));
    }

    private IEnumerator PurchasingObserveCoroutine(IInAppProcess process, IProduct product)
    {
        yield return new WaitUntil(() => process.IsDone == true);

        if (process.Result == Result.Succes)
        {
            //обновляем компоненты в UI
            var uiItem = _productItems[product.Id];
            var buttonBuy = uiItem.GetComponentInChildren<Button>();

            buttonBuy.interactable = product.IsBuy == false;
        }

        Debug.Log($"Покупка продукта {product.Id} закончилась с результатом {process.Result.ToString()}");
    }

    private IEnumerator InitInAppObserveCoroutione(IInAppProcess process, InApp inApp)
    {
        bool initSucces = false;
        var currentProcess = process;

        while (initSucces != true)
        {
            yield return new WaitUntil(() => currentProcess.IsDone == true);

            if (currentProcess.Result == Result.Succes)
            {
                initSucces = true;
                RefreshAllUiProductItems(inApp);
                Debug.Log("Инициализация прошла удачно!");
            }
            else
            {
                currentProcess = inApp.Inizialization();
                Debug.Log("Инициализация не пройдена. Перезапуск.");
            }
        }
    }

    private void RefreshAllUiProductItems(InApp inApp)
    {
        //Обновляем ui по параметрам, которые могут изменится на маркете - название продукта, описание, ценник
        List<IProduct> products = inApp.Product;

        foreach (var product in products)
        {
            string title = product.Title;
            string description = product.Description;
            string price = product.Price;

            var uiProduct = _productItems[product.Id];
            Text textTitle = uiProduct.GetComponentsInChildren<Text>()[0];
            Text textDescription = uiProduct.GetComponentsInChildren<Text>()[1];
            Text textPrice = uiProduct.GetComponentsInChildren<Text>()[2];

            textTitle.text = title;
            textDescription.text = description;
            textPrice.text = price;
        }
    }
}