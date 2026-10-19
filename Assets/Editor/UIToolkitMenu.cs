using UnityEditor;
using UnityEditor.SceneManagement;

namespace GamblingSeriousGame.Editor
{
    public static class UIToolkitMenu
    {
        [MenuItem("Game/UI/Open Base Page Scene")]
        public static void OpenBasePage()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/BasePageUIToolkit.unity");
        }

        [MenuItem("Game/UI/Edit Base Page")]
        public static void EditBasePage() => OpenAsset("Assets/UI/Screens/BasePage.uxml");

        [MenuItem("Game/UI/Edit Home Content")]
        public static void EditHome() => OpenAsset("Assets/UI/Elements/HomeContent.uxml");

        [MenuItem("Game/UI/Open UI Toolkit Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/PetCreationUIToolkit.unity");
        }

        [MenuItem("Game/UI/Edit Companion Screen")]
        public static void EditScreen() => OpenAsset("Assets/UI/Screens/PetSelection.uxml");

        [MenuItem("Game/UI/Edit Pet Creation Screen")]
        public static void EditPetCreation() => OpenAsset("Assets/UI/Screens/PetCreation.uxml");

        [MenuItem("Game/UI/Edit Shared Styles")]
        public static void EditStyles() => OpenAsset("Assets/UI/Themes/App.uss");

        [MenuItem("Game/UI/Open Style Guide")]
        public static void OpenStyleGuide() => OpenAsset("Assets/UI/Screens/StyleGuide.uxml");

        private static void OpenAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null) AssetDatabase.OpenAsset(asset);
        }
    }
}
