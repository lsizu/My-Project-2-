
using UnityEngine;

public class UIToggle : MonoBehaviour
{
    [SerializeField] private GameObject targetUI;

    public void Toggle()
    {
        targetUI.SetActive(!targetUI.activeSelf);
    }
}
