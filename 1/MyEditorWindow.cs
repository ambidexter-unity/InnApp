using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

public class MyEditorWindow : EditorWindow
{
    // получаем значок Preset и стиль для его отображения
    private static class Styles
    {
        public static GUIContent presetIcon = EditorGUIUtility.IconContent("Preset.Context");
        public static GUIStyle iconButton = new GUIStyle("IconButton");

    }

    private Editor m_SettingsEditor;
    private MyWindowSettings m_SerializedSettings;

    public string someSettings
    {
        get { return EditorPrefs.GetString("MyEditorWindow_SomeSettings"); }
        set { EditorPrefs.SetString("MyEditorWindow_SomeSettings", value); }
    }

    // Метод для открытия окна
    [MenuItem("Window/MyEditorWindow")]
    private static void OpenWindow()
    {
        GetWindow<MyEditorWindow>();
    }

    private void OnEnable()
    {
        // Создать ваши настройки сейчас и связанный с ним Инспектор
        // это позволяет создать только один пользовательский инспектор для настроек в окне и пресете.
        m_SerializedSettings = ScriptableObject.CreateInstance<MyWindowSettings>();
        m_SerializedSettings.Init(this);
        m_SettingsEditor = Editor.CreateEditor(m_SerializedSettings);
    }

    private void OnDisable()
    {
        Object.DestroyImmediate(m_SerializedSettings);
        Object.DestroyImmediate(m_SettingsEditor);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("My custom settings", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        // создаем кнопку «Preset» в конце строки «MyManager Settings».
        var buttonPosition = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, Styles.iconButton);

        if (EditorGUI.DropdownButton(buttonPosition, Styles.presetIcon, FocusType.Passive, Styles.iconButton))
        {
            // Создать экземпляр получателя. Это разрушает себя, когда появляется окно, поэтому вам не нужно сохранять ссылку на него.
            var presetReceiver = ScriptableObject.CreateInstance<MySettingsReceiver>();
            presetReceiver.Init(m_SerializedSettings, this);
            // Показать модальное окно PresetSelector. PresetReceiver обновляет ваши данные.
            PresetSelector.ShowSelector(m_SerializedSettings, null, true, presetReceiver);
        }
        EditorGUILayout.EndHorizontal();

        // Нарисовать настройки Инспектора по умолчанию и отловить любые изменения, внесенные в него.
        EditorGUI.BeginChangeCheck();
        m_SettingsEditor.OnInspectorGUI();

        if (EditorGUI.EndChangeCheck())
        {
            // Применяем изменения, сделанные в редакторе настроек, к нашему экземпляру.
            m_SerializedSettings.ApplySettings(this);
        }
    }
}