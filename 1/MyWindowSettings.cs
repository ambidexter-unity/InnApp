using UnityEngine;

// Временный ScriptableObject, используемый системой Preset
public class MyWindowSettings : ScriptableObject
{
    [SerializeField]
    private string m_SomeSettings;

    public void Init(MyEditorWindow window)
    {
        m_SomeSettings = window.someSettings;
    }

    public void ApplySettings(MyEditorWindow window)
    {
        window.someSettings = m_SomeSettings;
        window.Repaint();
    }
}