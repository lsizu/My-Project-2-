using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BackToCCSceneButton : MonoBehaviour
{
    private Button backButton;

    private void OnEnable()
    {
        backButton = GetComponent<Button>();
        backButton.onClick.AddListener(LoadCCScene);
    }

    private void OnDisable()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(LoadCCScene);
    }

    private void LoadCCScene()
    {
        const string sceneName = "CCScene";
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(sceneName + " is not available in the Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
