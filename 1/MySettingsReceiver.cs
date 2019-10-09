using UnityEditor.Presets;

// PresetSelector получатель для обновления EditorWindow с выбранными значениями.
public class MySettingsReceiver : PresetSelectorReceiver
{
    private Preset initialValues;
    private MyWindowSettings currentSettings;
    private MyEditorWindow currentWindow;

    public void Init(MyWindowSettings settings, MyEditorWindow window)
    {
        currentWindow = window;
        currentSettings = settings;
        initialValues = new Preset(currentSettings);
    }

    public override void OnSelectionChanged(Preset selection)
    {
        if (selection != null)
        {
            // Применить выбор к временным настройкам
            selection.ApplyTo(currentSettings);
        }
        else
        {
            // Ни один не был выбран. Примените Начальные значения обратно к временному выбору.
            initialValues.ApplyTo(currentSettings);
        }

        // Применяем новые временные настройки к нашему экземпляру менеджера
        currentSettings.ApplySettings(currentWindow);
    }

    public override void OnSelectionClosed(Preset selection)
    {
        // Изменение выбора вызова в последний раз, чтобы убедиться, что у вас есть последние значения выбора.
        OnSelectionChanged(selection);
        // Уничтожить приемник здесь, так что вам не нужно сохранять ссылку на него.
        DestroyImmediate(this);
    }
}