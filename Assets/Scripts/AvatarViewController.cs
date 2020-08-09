using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AvatarViewController : MonoBehaviour
{
    [SerializeField]
    Dropdown EffectDropdown;
    [SerializeField]
    GameObject EffectsParent;

    [SerializeField]
    RawImage DisplayWindow;

    [SerializeField]
    Camera ARCamera;

    [SerializeField]
    RectTransform maskRectTran;

    [SerializeField]
    float ZoomLevel = 2f; /* give a closer head view */

    float WindowSideLength   // this is the size of the mask frame
    {
        get
        {
            return maskRectTran.rect.width * ZoomLevel;
        }
    }

    Resolution resolution = default(Resolution);

    Dictionary<string, GameObject> EffectObject = new Dictionary<string, GameObject>();

    GameObject currentEffect;

    private void Start()
    {
        ARCamera = GameObject.Find("ARCamera")?.GetComponent<Camera>();
        EffectsParent = GameObject.Find("BNBARStuff/Faces/Face0");
        DisplayWindow = GetComponent<RawImage>();

        SetupDropdown();
        SetupButton();

        if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android)
        {
            ZoomLevel /= 2;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (resolution.width != Screen.width || resolution.height != Screen.height)
        {
            Debug.Log("Update: Resolution changed: " + Screen.width + "x" + Screen.height);
            resolution.width = Screen.width;
            resolution.height = Screen.height;
            UpdateTransforms();
        }
    }

    void SetupButton()
    {
        Button button = GetComponentInChildren<Button>();
        button.onClick.AddListener(() =>
        {
            if (EffectDropdown != null)
            {
                EffectDropdown.gameObject.SetActive(!EffectDropdown.gameObject.activeInHierarchy);
            }
        });
    }

    void SetupDropdown()
    {
        GameObject canvas = GameObject.Find("Canvas");
        EffectDropdown = canvas?.transform.Find("FilterDropdown")?.GetComponent<Dropdown>();

        if (EffectDropdown == null)
        {
            Debug.LogWarning("Dropdown not found");
            return;
        }
        List<string> effects = new List<string>();

        for (int i = 0; i < EffectsParent.transform.childCount; i++)
        {
            GameObject go = EffectsParent.transform.GetChild(i).gameObject;
            string objname = go.name;
            if (objname != "FaceMesh")
            {
                Debug.Log("dropdown ---->" + objname);
                effects.Add(objname);
                EffectObject[objname] = go;
            }
        }

        EffectDropdown.gameObject.SetActive(false);

        EffectDropdown.ClearOptions();
        EffectDropdown.AddOptions(effects);
        EffectDropdown.value = 0;
        EffectDropdown.onValueChanged.AddListener(DropDownSelected);
        DropDownSelected(0);
    }

    void DropDownSelected(int num)
    {
        string effName = EffectDropdown.options[num].text;
        Debug.LogWarning(effName + " was selected");
        ChangeEffect(effName);
    }

    void UpdateTransforms()
    {
        if (ARCamera == null) return;
        var curRenderTexture = ARCamera.targetTexture;
        // Copy the predefined render texture to a new instance
        RenderTexture renderTexture = new RenderTexture(curRenderTexture);
        renderTexture.width = resolution.width;
        renderTexture.height = resolution.height;

        curRenderTexture.Release();
        ARCamera.targetTexture = renderTexture;

        DisplayWindow.texture = renderTexture;
        DisplayWindow.rectTransform.sizeDelta = new Vector2(GetScaledWidth(), GetScaledHeight());
    }

    void ChangeEffect(string effName)
    {
        if (currentEffect != null)
        {
            currentEffect.SetActive(false);
        }

        currentEffect = EffectObject[effName];
        if (currentEffect != null)
        {
            currentEffect.SetActive(true);
        }
    }

    float GetScaledWidth()
    {
        float length = WindowSideLength;
        if (resolution.width > resolution.height)
        {
            // height == WindowSideLength
            float ratio = (float)resolution.height / (float)resolution.width;
            length = WindowSideLength / ratio;
        }

        Debug.LogWarning("Scaled width = " + length);
        return length;
    }

    float GetScaledHeight()
    {
        float length = WindowSideLength;
        if (resolution.width <= resolution.height)
        {
            float ratio = (float)resolution.height / (float)resolution.width;
            length = WindowSideLength * ratio;
        }
        Debug.LogWarning("Scaled height = " + length);
        return length;
    }

}