using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DecorationPlacementController : MonoBehaviour
{
    [Serializable]
    public sealed class DecorationItem
    {
        public string id;
        public GameObject prefab;
        public Button applyButton;
        public GameObject appliedButton;
    }

    [Header("UI / Data")]
    [SerializeField] private GameObject _detailUI;
    [SerializeField] private DetailPanelAnimator _detailAnimator;
    [SerializeField] private DecorationPlacementDataSet _dataSet;
    [SerializeField] private DecorationItem[] _items = Array.Empty<DecorationItem>();

    [Header("Placement")]
    [SerializeField] private LayerMask _placementMask = ~0;
    [SerializeField] private float _rayDistance = 500f;
    [SerializeField] private Color _previewEmission = new Color(0.05f, 0.75f, 1f, 1f);
    [SerializeField] private float _emissionIntensity = 4f;
    [SerializeField] private float _placementFlashMultiplier = 2.5f;
    [SerializeField] private float _placementFlashHold = 0.12f;
    [SerializeField] private float _buildFadeDuration = 1.4f;

    private const string DecorationResetVersionKey = "decoration_save_reset_version";
    private const int DecorationResetVersion = 1;

    private Camera _camera;
    private GameObject _preview;
    private Material[][] _originalMaterials;
    private Renderer[] _previewRenderers;
    private int _placingIndex = -1;
    private int _startedFrame;
    private bool _detailWasActive;

    private void Awake()
    {
        _camera = Camera.main;
        if (_dataSet == null) return;
        if (PlayerPrefs.GetInt(DecorationResetVersionKey, 0) < DecorationResetVersion)
        {
            _dataSet.ClearSavedState();
            PlayerPrefs.SetInt(DecorationResetVersionKey, DecorationResetVersion);
            PlayerPrefs.Save();
            Debug.Log("[Decoration] Saved placements were reset.");
        }
        _dataSet.Load();
        for (int i = 0; i < _items.Length; i++)
        {
            int index = i;
            if (_items[i].applyButton != null)
                _items[i].applyButton.onClick.AddListener(() => BeginPlacement(index));
        }
        RestorePlacedObjects();
        RefreshButtons();
        _detailWasActive = _detailUI != null && _detailUI.activeInHierarchy;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _items.Length; i++)
            if (_items[i].applyButton != null) _items[i].applyButton.onClick.RemoveAllListeners();
    }

    private void Update()
    {
        bool detailActive = _detailUI != null && _detailUI.activeInHierarchy;
        if (detailActive && !_detailWasActive) RefreshButtons();
        _detailWasActive = detailActive;

        if (_preview == null) return;
        UpdatePreviewPosition();
        if (Time.frameCount == _startedFrame || !PointerPressedThisFrame() || IsPointerOverUI()) return;
        ConfirmPlacement();
    }

    private void BeginPlacement(int index)
    {
        if (_dataSet == null || index < 0 || index >= _items.Length || _dataSet.IsApplied(index)) return;
        GameObject prefab = _items[index].prefab;
        if (prefab == null) { Debug.LogError($"[Decoration] Prefab is missing for {_items[index].id}.", this); return; }

        if (_preview != null) Destroy(_preview);
        _placingIndex = index;
        _startedFrame = Time.frameCount;
        _preview = Instantiate(prefab);
        _preview.name = prefab.name + " (Placement Preview)";
        foreach (Collider collider in _preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        PrepareEmissionPreview();
        if (_detailAnimator != null) _detailAnimator.HideAnimated();
        else if (_detailUI != null) _detailUI.SetActive(false);
        UpdatePreviewPosition();
    }

    private void UpdatePreviewPosition()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null || _preview == null) return;
        Ray ray = _camera.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, _rayDistance, _placementMask, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool found = false;
        Vector3 position = _preview.transform.position;
        foreach (RaycastHit hit in hits)
        {
            if (Mathf.Abs(hit.normal.y) < 0.45f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            position = hit.point;
            found = true;
        }
        if (found) _preview.transform.position = position;
    }

    private void ConfirmPlacement()
    {
        int index = _placingIndex;
        GameObject placed = _preview;
        _preview = null;
        _placingIndex = -1;
        foreach (Collider collider in placed.GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        _dataSet.SetApplied(index, placed.transform.position, placed.transform.rotation);
        Renderer[] renderers = _previewRenderers;
        Material[][] originals = _originalMaterials;
        _previewRenderers = null;
        _originalMaterials = null;
        StartCoroutine(FinishBuildEffect(placed, renderers, originals));
        RefreshButtons();
    }

    private void PrepareEmissionPreview()
    {
        _previewRenderers = _preview.GetComponentsInChildren<Renderer>(true);
        _originalMaterials = new Material[_previewRenderers.Length][];
        Color emission = _previewEmission * _emissionIntensity;
        for (int r = 0; r < _previewRenderers.Length; r++)
        {
            _originalMaterials[r] = _previewRenderers[r].sharedMaterials;
            Material[] glowing = _previewRenderers[r].materials;
            foreach (Material material in glowing)
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.Lerp(material.GetColor("_BaseColor"), _previewEmission, 0.35f));
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.Lerp(material.color, _previewEmission, 0.35f));
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            }
        }
    }

    private IEnumerator FinishBuildEffect(GameObject placed, Renderer[] renderers, Material[][] originals)
    {
        if (placed == null || renderers == null || originals == null) yield break;

        float peakIntensity = _emissionIntensity * Mathf.Max(1f, _placementFlashMultiplier);
        SetEmissionIntensity(renderers, peakIntensity);
        if (_placementFlashHold > 0f)
            yield return new WaitForSeconds(_placementFlashHold);

        float elapsed = 0f;
        while (elapsed < _buildFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _buildFadeDuration));
            float eased = t * t * (3f - 2f * t);
            SetEmissionIntensity(renderers, Mathf.Lerp(peakIntensity, 0f, eased));
            yield return null;
        }
        for (int r = 0; r < renderers.Length; r++)
            if (renderers[r] != null) renderers[r].sharedMaterials = originals[r];
    }

    private void SetEmissionIntensity(Renderer[] renderers, float intensity)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            foreach (Material material in renderer.materials)
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", _previewEmission * intensity);
        }
    }

    private void RestorePlacedObjects()
    {
        for (int i = 0; i < _items.Length && i < _dataSet.Items.Count; i++)
        {
            if (!_dataSet.IsApplied(i) || _items[i].prefab == null) continue;
            DecorationPlacementDataSet.ItemState state = _dataSet.Items[i];
            GameObject placed = Instantiate(_items[i].prefab, state.position, Quaternion.Euler(state.eulerAngles));
            placed.name = _items[i].prefab.name + " (Placed)";
        }
    }

    public void RefreshButtons()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            bool applied = _dataSet != null && _dataSet.IsApplied(i);
            if (_items[i].applyButton != null) _items[i].applyButton.gameObject.SetActive(!applied);
            if (_items[i].appliedButton != null) _items[i].appliedButton.SetActive(applied);
        }
    }

    private static Vector2 PointerPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    private static bool PointerPressedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return EventSystem.current.IsPointerOverGameObject(touchId);
        }
        return EventSystem.current.IsPointerOverGameObject();
    }
}
