
using BDT.GUI.Helpers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Popup window for setting resource order position.
/// Allows user to type a specific order number and move the item there.
/// </summary>
public class SetOrderPopupWindow : PopupWindowContent
{
    private int _currentIndex;
    private int _maxCount;
    private int _orderValue;
    private System.Action<int> _onConfirm;

    public static void Show(Rect buttonRect, int currentIndex, int maxCount, System.Action<int> onConfirm)
    {
        var popup = new SetOrderPopupWindow
        {
            _currentIndex = currentIndex,
            _maxCount = maxCount,
            _orderValue = currentIndex + 1, // 1-based display
            _onConfirm = onConfirm
        };
        
        PopupWindow.Show(buttonRect, popup);
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(180, 75);
    }

    public override void OnGUI(Rect rect)
    {
        GUILayout.Space(4);
        GUILayout.Label($"Move to position (1-{_maxCount}):", EditorStyles.miniLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // Order input field
        _orderValue = EditorGUILayout.IntField(_orderValue, GUILayout.Width(60));
        _orderValue = Mathf.Clamp(_orderValue, 1, _maxCount);
        
        // Set button
        if (GUILayout.Button("Set", GUILayout.Width(50)))
        {
            _onConfirm?.Invoke(_orderValue);
            editorWindow.Close();
        }
        
        // Cancel button
        if (GUILayout.Button("×", GUILayout.Width(24)))
        {
            editorWindow.Close();
        }
        EditorGUILayout.EndHorizontal();
        
        // Info about what will happen
        GUILayout.Space(2);
        if (_orderValue != _currentIndex + 1)
        {
            string direction = _orderValue < _currentIndex + 1 ? "↑ Move up" : "↓ Move down";
            int delta = Mathf.Abs(_orderValue - (_currentIndex + 1));
            
            var infoStyle = new GUIStyle(EditorStyles.miniLabel);
            infoStyle.normal.textColor = UIColors.Cyan;
            GUILayout.Label($"{direction} by {delta} position{(delta > 1 ? "s" : "")}", infoStyle);
        }
        else
        {
            var infoStyle = new GUIStyle(EditorStyles.miniLabel);
            infoStyle.normal.textColor = UIColors.GrayLine;
            GUILayout.Label("(current position)", infoStyle);
        }
    }
}
