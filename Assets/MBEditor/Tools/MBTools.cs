using BDT.GUI.Helpers;
using UnityEngine;

public class MBTools
{
    private int _toolbarOption;
    private string[] _toolbarTexts = { "Scenes Importer" };
    private MBSceneImporter _sceneImporter;

    public void DrawToolsGUI(MBModule _currentModule)
    {
        MBToolsGUIHeader.SceneTools = UIHelpers.BlockElementStart("Scene Tools", MBToolsGUIHeader.SceneTools);
        if (MBToolsGUIHeader.SceneTools)
        {
            _toolbarOption = GUILayout.Toolbar(_toolbarOption, _toolbarTexts, GUILayout.Height(32));
            GUILayout.Space(2);
            GUIHelpers.DrawUILine(Color.grey);

            switch (_toolbarOption)
            {
                case 0:
                    if (_sceneImporter == null)
                        _sceneImporter = new MBSceneImporter();

                    _sceneImporter.DrawGUI(_currentModule);
                    break;
            }


            UIHelpers.BlockElementEnd();
        }
    }
}