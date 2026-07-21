using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>기존 UI 크기를 바꾸지 않고 클릭/드래그 위치를 0~5 값으로 바꾼다.</summary>
public sealed class SurveyValueSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _count;
    [SerializeField, Range(0, 5)] private int _value = 3;

    public int Value => _value;

    public void Configure(Image fill, TMP_Text count)
    {
        _fill = fill;
        _count = count;
        SetValue(_value);
    }

    public void SetValue(int value)
    {
        _value = Mathf.Clamp(value, 0, 5);
        if (_fill != null)
        {
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.fillAmount = _value / 5f;
        }
        if (_count != null) _count.text = _value + "/5";
    }

    public void OnPointerDown(PointerEventData eventData) => SetFromPointer(eventData);
    public void OnDrag(PointerEventData eventData) => SetFromPointer(eventData);

    private void SetFromPointer(PointerEventData eventData)
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null || rect.rect.width <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
        float normalized = Mathf.Clamp01((local.x - rect.rect.xMin) / rect.rect.width);
        SetValue(Mathf.RoundToInt(normalized * 5f));
    }
}
